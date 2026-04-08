using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Cashflow.Shared.Identity.Keycloak
{
    public static class KeycloakAuthExtensions
    {
        public static IServiceCollection AddKeycloakAuthentication(
            this IServiceCollection services,
            IConfiguration configuration,
            bool requireHttpsMetadata = true)
        {
            var authority = configuration["Keycloak:Authority"];
            var audience = configuration["Keycloak:Audience"];

            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = authority;
                    options.Audience = audience;
                    options.MapInboundClaims = false;
                    options.RequireHttpsMetadata = requireHttpsMetadata;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(1),
                        RoleClaimType = "roles"
                    };

                    if (!requireHttpsMetadata)
                    {
                        options.TokenValidationParameters.IssuerValidator = (issuer, securityToken, parameters) =>
                            ValidateLocalIssuer(issuer, securityToken, authority, parameters);
                    }
                });

            return services;
        }

        private static string ValidateLocalIssuer(
            string issuer,
            SecurityToken securityToken,
            string? authority,
            TokenValidationParameters parameters)
        {
            var effectiveIssuer = !string.IsNullOrWhiteSpace(issuer)
                ? issuer
                : securityToken?.Issuer;

            if (string.IsNullOrWhiteSpace(effectiveIssuer))
            {
                throw new SecurityTokenInvalidIssuerException("Token issuer is missing.");
            }

            if (MatchesExpectedIssuer(effectiveIssuer, parameters.ValidIssuer) ||
                parameters.ValidIssuers?.Any(validIssuer => MatchesExpectedIssuer(effectiveIssuer, validIssuer)) == true)
            {
                return effectiveIssuer;
            }

            if (IsEquivalentLocalAuthority(effectiveIssuer, authority))
            {
                return effectiveIssuer;
            }

            throw new SecurityTokenInvalidIssuerException($"The issuer '{effectiveIssuer}' is invalid.");
        }

        private static bool MatchesExpectedIssuer(string issuer, string? validIssuer)
        {
            return !string.IsNullOrWhiteSpace(validIssuer) &&
                string.Equals(issuer, validIssuer, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsEquivalentLocalAuthority(string issuer, string? authority)
        {
            if (!Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri) ||
                !Uri.TryCreate(authority, UriKind.Absolute, out var authorityUri))
            {
                return false;
            }

            return string.Equals(issuerUri.Scheme, authorityUri.Scheme, StringComparison.OrdinalIgnoreCase) &&
                issuerUri.Port == authorityUri.Port &&
                string.Equals(
                    issuerUri.AbsolutePath.TrimEnd('/'),
                    authorityUri.AbsolutePath.TrimEnd('/'),
                    StringComparison.OrdinalIgnoreCase) &&
                IsLocalHost(issuerUri.Host) &&
                IsLocalHost(authorityUri.Host);
        }

        private static bool IsLocalHost(string host)
        {
            return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase);
        }
    }
}
