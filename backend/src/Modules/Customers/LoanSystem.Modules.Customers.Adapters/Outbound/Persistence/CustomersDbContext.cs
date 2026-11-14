using LoanSystem.Common.Adapters.Persistence;
using LoanSystem.Modules.Customers.Adapters.Outbound.Persistence.Configurations;
using LoanSystem.Modules.Customers.Core.Domain;
using LoanSystem.Modules.Customers.Core.Ports.Outbound;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Npgsql.NameTranslation;

namespace LoanSystem.Modules.Customers.Adapters.Outbound.Persistence;

internal sealed class CustomersDbContext : ModuleDbContext, ICustomersUnitOfWork, ICustomersOutbox
{
    internal static readonly NpgsqlNullNameTranslator EnumNameTranslator = new();

    public CustomersDbContext(DbContextOptions<CustomersDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerDocument> CustomerDocuments => Set<CustomerDocument>();
    public DbSet<KycVerification> KycVerifications => Set<KycVerification>();
    public DbSet<ConsentText> ConsentTexts => Set<ConsentText>();
    public DbSet<CustomerConsent> CustomerConsents => Set<CustomerConsent>();
    public DbSet<OtpCode> OtpVerifications => Set<OtpCode>();
    public DbSet<CustomerBankAccount> BankAccounts => Set<CustomerBankAccount>();

    [DbFunction("unaccent", IsBuiltIn = false)]
    public static string Unaccent(string value) => throw new NotSupportedException();

    internal static void MapEnums(NpgsqlDbContextOptionsBuilder npgsql)
    {
        npgsql.MapEnum<PersonType>("person_type", "public", EnumNameTranslator);
        npgsql.MapEnum<DocumentType>("document_type", "public", EnumNameTranslator);
        npgsql.MapEnum<KycStatus>("kyc_status_type", "public", EnumNameTranslator);
        npgsql.MapEnum<AmlRiskLevel>("aml_risk_level", "public", EnumNameTranslator);
        npgsql.MapEnum<KycCheckType>("kyc_check_type", "public", EnumNameTranslator);
        npgsql.MapEnum<KycCheckResult>("kyc_check_result", "public", EnumNameTranslator);
        npgsql.MapEnum<DocumentCategory>("customer_document_category", "public", EnumNameTranslator);
        npgsql.MapEnum<ConsentPurpose>("consent_purpose", "public", EnumNameTranslator);
        npgsql.MapEnum<OtpChannel>("notification_channel", "public", EnumNameTranslator);
        npgsql.MapEnum<OtpPurpose>("otp_purpose", "public", EnumNameTranslator);
        npgsql.MapEnum<BankAccountType>("bank_account_type", "public", EnumNameTranslator);
        npgsql.MapEnum<BankAccountStatus>("bank_account_status", "public", EnumNameTranslator);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresEnum<PersonType>("public", "person_type", EnumNameTranslator);
        modelBuilder.HasPostgresEnum<DocumentType>("public", "document_type", EnumNameTranslator);
        modelBuilder.HasPostgresEnum<KycStatus>("public", "kyc_status_type", EnumNameTranslator);
        modelBuilder.HasPostgresEnum<AmlRiskLevel>("public", "aml_risk_level", EnumNameTranslator);
        modelBuilder.HasPostgresEnum<KycCheckType>("public", "kyc_check_type", EnumNameTranslator);
        modelBuilder.HasPostgresEnum<KycCheckResult>("public", "kyc_check_result", EnumNameTranslator);
        modelBuilder.HasPostgresEnum<DocumentCategory>("public", "customer_document_category", EnumNameTranslator);
        modelBuilder.HasPostgresEnum<ConsentPurpose>("public", "consent_purpose", EnumNameTranslator);
        modelBuilder.HasPostgresEnum<OtpChannel>("public", "notification_channel", EnumNameTranslator);
        modelBuilder.HasPostgresEnum<OtpPurpose>("public", "otp_purpose", EnumNameTranslator);
        modelBuilder.HasPostgresEnum<BankAccountType>("public", "bank_account_type", EnumNameTranslator);
        modelBuilder.HasPostgresEnum<BankAccountStatus>("public", "bank_account_status", EnumNameTranslator);

        modelBuilder.HasDbFunction(typeof(CustomersDbContext).GetMethod(nameof(Unaccent), [typeof(string)])!)
            .HasName("unaccent");

        modelBuilder.ApplyConfiguration(new CustomerConfiguration());
        modelBuilder.ApplyConfiguration(new CustomerDocumentConfiguration());
        modelBuilder.ApplyConfiguration(new KycVerificationConfiguration());
        modelBuilder.ApplyConfiguration(new ConsentTextConfiguration());
        modelBuilder.ApplyConfiguration(new CustomerConsentConfiguration());
        modelBuilder.ApplyConfiguration(new OtpCodeConfiguration());
        modelBuilder.ApplyConfiguration(new CustomerBankAccountConfiguration());
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pgEx)
        {
            throw new DuplicateRecordException(pgEx.ConstraintName, ex);
        }
    }
}
