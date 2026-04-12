namespace Azure.Smoke.Tests
{
    internal static class AzureSmokeSettings
    {
        public static string? GetOptional(string environmentVariableName)
        {
            var value = Environment.GetEnvironmentVariable(environmentVariableName);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        public static Uri? GetOptionalUri(string environmentVariableName)
        {
            var value = GetOptional(environmentVariableName);
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                return uri;
            }

            throw new InvalidOperationException(
                $"Environment variable '{environmentVariableName}' must contain a valid absolute URI.");
        }
    }
}
