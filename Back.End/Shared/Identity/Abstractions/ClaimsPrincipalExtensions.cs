using System.Security.Claims;
using System.Text.Json;

namespace Cashflow.Shared.Identity.Abstractions
{
    public static class ClaimsPrincipalExtensions
    {
        public static bool HasScope(this ClaimsPrincipal user, string expectedScope)
        {
            ArgumentNullException.ThrowIfNull(user);
            ArgumentException.ThrowIfNullOrWhiteSpace(expectedScope);

            return GetScopes(user).Any(scope =>
                string.Equals(scope, expectedScope, StringComparison.OrdinalIgnoreCase));
        }

        public static bool HasRole(this ClaimsPrincipal user, string expectedRole)
        {
            ArgumentNullException.ThrowIfNull(user);
            ArgumentException.ThrowIfNullOrWhiteSpace(expectedRole);

            return GetRoles(user).Any(role =>
                string.Equals(role, expectedRole, StringComparison.OrdinalIgnoreCase));
        }

        private static IReadOnlyCollection<string> GetScopes(this ClaimsPrincipal user)
        {
            ArgumentNullException.ThrowIfNull(user);

            var scopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var claim in user.Claims.Where(claim => claim.Type is "scope" or "scp"))
            {
                AddDelimitedValues(scopes, claim.Value);
            }

            return scopes.ToArray();
        }

        private static IReadOnlyCollection<string> GetRoles(this ClaimsPrincipal user)
        {
            ArgumentNullException.ThrowIfNull(user);

            var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var claim in user.Claims.Where(claim => claim.Type is "role" or "roles"))
            {
                AddClaimValue(roles, claim.Value);
            }

            foreach (var claim in user.Claims.Where(claim => claim.Type == "realm_access"))
            {
                AddRealmAccessRoles(roles, claim.Value);
            }

            foreach (var claim in user.Claims.Where(claim => claim.Type == "resource_access"))
            {
                AddResourceAccessRoles(roles, claim.Value);
            }

            return roles.ToArray();
        }

        private static void AddClaimValue(HashSet<string> values, string? rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return;
            }

            var trimmedValue = rawValue.Trim();

            if (trimmedValue.StartsWith("[", StringComparison.Ordinal))
            {
                AddJsonArrayValues(values, trimmedValue);
                return;
            }

            AddDelimitedValues(values, trimmedValue);
        }

        private static void AddDelimitedValues(HashSet<string> values, string? rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return;
            }

            foreach (var value in rawValue.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                values.Add(value);
            }
        }

        private static void AddRealmAccessRoles(HashSet<string> values, string? rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return;
            }

            try
            {
                using var document = JsonDocument.Parse(rawValue);
                if (!document.RootElement.TryGetProperty("roles", out var rolesElement) ||
                    rolesElement.ValueKind != JsonValueKind.Array)
                {
                    return;
                }

                foreach (var item in rolesElement.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        AddDelimitedValues(values, item.GetString());
                    }
                }
            }
            catch (JsonException)
            {
                // Claim format unexpected; ignore and keep the original claims as the source of truth.
            }
        }

        private static void AddResourceAccessRoles(HashSet<string> values, string? rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return;
            }

            try
            {
                using var document = JsonDocument.Parse(rawValue);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return;
                }

                foreach (var clientEntry in document.RootElement.EnumerateObject())
                {
                    if (!clientEntry.Value.TryGetProperty("roles", out var rolesElement) ||
                        rolesElement.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var item in rolesElement.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                        {
                            AddDelimitedValues(values, item.GetString());
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Claim format unexpected; ignore and keep the original claims as the source of truth.
            }
        }

        private static void AddJsonArrayValues(HashSet<string> values, string rawValue)
        {
            try
            {
                using var document = JsonDocument.Parse(rawValue);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return;
                }

                foreach (var item in document.RootElement.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        AddDelimitedValues(values, item.GetString());
                    }
                }
            }
            catch (JsonException)
            {
                AddDelimitedValues(values, rawValue);
            }
        }
    }
}
