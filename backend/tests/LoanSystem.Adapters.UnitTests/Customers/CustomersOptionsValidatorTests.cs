using System.Security.Cryptography;
using LoanSystem.Modules.Customers.Adapters;
using LoanSystem.Modules.Customers.Core.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace LoanSystem.Adapters.UnitTests.Customers;

public class CustomersOptionsValidatorTests
{
    private static IConfiguration BuildConfig(Dictionary<string, string?>? values = null) =>
        new ConfigurationBuilder().AddInMemoryCollection(values ?? []).Build();

    private static IHostEnvironment Env(string name) =>
        new TestHostEnv { EnvironmentName = name };

    [Fact]
    public void Validate_ValidDefaultsInDevelopment_Succeeds()
    {
        var validator = new CustomersOptionsValidator(BuildConfig(), Env(Environments.Development));
        var result = validator.Validate(null, new CustomersOptions());

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_RejectsInvalidCoolingWindowAndMissingS3Bucket()
    {
        var validator = new CustomersOptionsValidator(BuildConfig(), Env(Environments.Development));

        var tooShortCooling = new CustomersOptions { BankAccounts = new BankAccountsOptions { CoolingPeriodHours = 12 } };
        validator.Validate(null, tooShortCooling).Failed.ShouldBeTrue();

        var s3NoBucket = new CustomersOptions { Storage = new StorageOptions { Provider = "S3", Bucket = "" } };
        validator.Validate(null, s3NoBucket).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_Production_RejectsFakeVerifierAndMissingSecrets_AndAcceptsCompleteConfig()
    {
        var validProdConfig = BuildConfig(new Dictionary<string, string?>
        {
            ["Customers:Otp:Pepper"] = "production-otp-pepper-at-least-16-chars",
            ["Customers:FieldProtection:Key"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            ["Customers:FieldProtection:HmacKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            ["Customers:Storage:SigningSecret"] = "production-url-signing-secret"
        });

        var prodEnv = Env(Environments.Production);
        var validator = new CustomersOptionsValidator(validProdConfig, prodEnv);

        // Fake verifier rejected in Production
        var fakeInProd = new CustomersOptions { IdentityVerifier = new IdentityVerifierOptions { Provider = "Fake" } };
        var fakeRes = validator.Validate(null, fakeInProd);
        fakeRes.Failed.ShouldBeTrue();
        fakeRes.Failures.ShouldContain(f => f.Contains("Fake", StringComparison.OrdinalIgnoreCase));

        // Non-Fake verifier with complete secrets passes
        var validProdOpts = new CustomersOptions { IdentityVerifier = new IdentityVerifierOptions { Provider = "ReniecHttp" } };
        validator.Validate(null, validProdOpts).Succeeded.ShouldBeTrue();

        // Missing secrets rejected in Production (Otp:Pepper + FieldProtection Key/HmacKey = 2 errors for Local storage; +1 for S3)
        var emptyProdValidator = new CustomersOptionsValidator(BuildConfig(), prodEnv);
        var missingSecrets = emptyProdValidator.Validate(null, validProdOpts);
        missingSecrets.Failed.ShouldBeTrue();
        missingSecrets.Failures.Count().ShouldBeGreaterThanOrEqualTo(2);

        var s3ProdOpts = new CustomersOptions
        {
            IdentityVerifier = new IdentityVerifierOptions { Provider = "ReniecHttp" },
            Storage = new StorageOptions { Provider = "S3", Bucket = "prod-kyc-docs" }
        };
        var s3MissingSecrets = emptyProdValidator.Validate(null, s3ProdOpts);
        s3MissingSecrets.Failed.ShouldBeTrue();
        s3MissingSecrets.Failures.Count().ShouldBeGreaterThanOrEqualTo(3);
    }

    private sealed class TestHostEnv : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "LoanSystem.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
