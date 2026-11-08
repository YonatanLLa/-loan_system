using Microsoft.Extensions.Configuration;

namespace LoanSystem.Common.Adapters.Configuration;

public static class ConfigurationExtensions
{
    /// <summary>
    /// Lee una cadena de conexión y falla de inmediato si no existe. A propósito no hay
    /// valor por defecto en código: los valores locales van en appsettings.Development.json o en variables de entorno.
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

    /// <summary>Lee un valor de configuración y falla de inmediato si no existe.</summary>
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
