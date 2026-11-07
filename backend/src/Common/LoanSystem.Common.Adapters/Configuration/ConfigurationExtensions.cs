using Microsoft.Extensions.Configuration;

namespace LoanSystem.Common.Adapters.Configuration;

public static class ConfigurationExtensions
{
    /// <summary>
    /// Reads a connection string and fails fast when it is missing. There is intentionally no
    /// fallback in code: local values live in appsettings.Development.json or environment variables.
    /// </summary>
    public static string GetRequiredConnectionString(this IConfiguration configuration, string name)
    {
        var value = configuration.GetConnectionString(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Connection string '{name}' is not configured. Set 'ConnectionStrings:{name}' in appsettings.Development.json " +
                $"or the environment variable 'ConnectionStrings__{name}'.");
        }

        return value;
    }

    /// <summary>Reads a configuration value and fails fast when it is missing.</summary>
    public static string GetRequiredValue(this IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Configuration value '{key}' is not configured. Set it in appsettings.Development.json " +
                $"or the environment variable '{key.Replace(":", "__", StringComparison.Ordinal)}'.");
        }

        return value;
    }
}
