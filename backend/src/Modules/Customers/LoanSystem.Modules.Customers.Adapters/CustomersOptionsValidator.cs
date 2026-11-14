using LoanSystem.Modules.Customers.Core.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LoanSystem.Modules.Customers.Adapters;

internal sealed class CustomersOptionsValidator : IValidateOptions<CustomersOptions>
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public CustomersOptionsValidator(IConfiguration configuration, IHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, CustomersOptions options)
    {
        var errors = new List<string>();

        if (options.BankAccounts.CoolingPeriodHours is < 24 or > 48)
        {
            errors.Add("Customers:BankAccounts:CoolingPeriodHours must be between 24 and 48.");
        }

        if (options.BankAccounts.MaxActivePerCustomer <= 0)
        {
            errors.Add("Customers:BankAccounts:MaxActivePerCustomer must be greater than 0.");
        }

        if (options.Kyc.ValidityMonths <= 0)
        {
            errors.Add("Customers:Kyc:ValidityMonths must be greater than 0.");
        }

        if (options.Kyc.ExpiringSoonDays < 0)
        {
            errors.Add("Customers:Kyc:ExpiringSoonDays cannot be negative.");
        }

        if (options.Documents.MaxFileSizeMb <= 0)
        {
            errors.Add("Customers:Documents:MaxFileSizeMb must be greater than 0.");
        }

        if (options.Documents.SignedUrlTtlSeconds <= 0)
        {
            errors.Add("Customers:Documents:SignedUrlTtlSeconds must be greater than 0.");
        }

        if (options.Documents.AllowedTypes.Length == 0)
        {
            errors.Add("Customers:Documents:AllowedTypes must specify at least one MIME type.");
        }

        if (options.Otp.Length is < 4 or > 8)
        {
            errors.Add("Customers:Otp:Length must be between 4 and 8.");
        }

        if (options.Otp.TtlSeconds <= 0 || options.Otp.MaxAttempts <= 0 || options.Otp.MaxSendsPerHour <= 0)
        {
            errors.Add("Customers:Otp TTL, MaxAttempts, and MaxSendsPerHour must be positive.");
        }

        var storageProvider = options.Storage.Provider?.Trim() ?? string.Empty;
        if (!string.Equals(storageProvider, "Local", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(storageProvider, "S3", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Unsupported Customers:Storage:Provider '{options.Storage.Provider}'. Expected 'Local' or 'S3'.");
        }

        if (string.Equals(storageProvider, "S3", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(options.Storage.Bucket))
        {
            errors.Add("Customers:Storage:Bucket is required when Storage:Provider is 'S3'.");
        }

        if (_environment.IsProduction() &&
            string.Equals(options.IdentityVerifier.Provider?.Trim(), "Fake", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Customers:IdentityVerifier:Provider cannot be 'Fake' in Production.");
        }

        var requireProductionSecrets =
            !_environment.IsDevelopment() &&
            !string.Equals(_environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase);

        if (requireProductionSecrets)
        {
            var pepper = _configuration["Customers:Otp:Pepper"];
            if (string.IsNullOrWhiteSpace(pepper) || pepper.Length < 16)
            {
                errors.Add("Customers:Otp:Pepper (at least 16 chars) is required in non-development environments.");
            }

            if (string.IsNullOrWhiteSpace(_configuration["Customers:FieldProtection:HmacKey"]) ||
                string.IsNullOrWhiteSpace(_configuration["Customers:FieldProtection:Key"]))
            {
                errors.Add("Customers:FieldProtection:Key and HmacKey must be configured in non-development environments.");
            }

            if (string.Equals(storageProvider, "S3", StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(_configuration["Customers:Storage:AccessKey"]) ||
                 string.IsNullOrWhiteSpace(_configuration["Customers:Storage:SecretKey"])))
            {
                errors.Add("Customers:Storage:AccessKey and SecretKey must be configured for S3 storage in non-development environments.");
            }
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
