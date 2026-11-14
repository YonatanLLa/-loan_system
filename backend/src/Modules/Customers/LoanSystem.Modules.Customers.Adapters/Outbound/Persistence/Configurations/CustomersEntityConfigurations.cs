using LoanSystem.Common.Adapters.Persistence;
using LoanSystem.Common.Core.Domain;
using LoanSystem.Modules.Customers.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoanSystem.Modules.Customers.Adapters.Outbound.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.PersonType).HasColumnName("person_type").IsRequired();

        builder.OwnsOne(c => c.Document, doc =>
        {
            doc.Property(d => d.Type).HasColumnName("doc_type").IsRequired();
            doc.Property(d => d.Number).HasColumnName("doc_number").HasMaxLength(20).IsRequired();
        });

        builder.Property(c => c.FullName).HasColumnName("full_name").HasMaxLength(200).IsRequired();
        builder.Property(c => c.BirthDate).HasColumnName("birth_date");
        builder.Property(c => c.Email).HasColumnName("email").HasMaxLength(150);
        builder.Property(c => c.EmailVerifiedAt).HasColumnName("email_verified_at");
        builder.Property(c => c.PhonePrimary).HasColumnName("phone_primary").HasMaxLength(20);
        builder.Property(c => c.PhoneSecondary).HasColumnName("phone_secondary").HasMaxLength(20);
        builder.Property(c => c.PhoneVerifiedAt).HasColumnName("phone_verified_at");
        builder.Property(c => c.Address).HasColumnName("address");
        builder.Property(c => c.District).HasColumnName("district").HasMaxLength(100);

        builder.Property(c => c.MonthlyNetIncome)
            .HasColumnName("monthly_net_income")
            .HasPrecision(18, 4)
            .HasConversion(m => m.Amount, a => Money.From(a, Money.DefaultCurrency));

        builder.Property(c => c.Occupation).HasColumnName("occupation").HasMaxLength(150);
        builder.Property(c => c.EmployerName).HasColumnName("employer_name").HasMaxLength(200);
        builder.Property(c => c.WorkSeniorityMonths).HasColumnName("work_seniority_months").HasColumnType("smallint");

        builder.Property(c => c.KycStatus).HasColumnName("kyc_status").IsRequired();
        builder.Property(c => c.KycSubmittedAt).HasColumnName("kyc_submitted_at");
        builder.Property(c => c.KycVerifiedAt).HasColumnName("kyc_verified_at");
        builder.Property(c => c.KycVerifiedBy).HasColumnName("kyc_verified_by");
        builder.Property(c => c.KycExpiresAt).HasColumnName("kyc_expires_at");
        builder.Property(c => c.KycRejectedReason).HasColumnName("kyc_rejected_reason");
        builder.Property(c => c.KycNotes).HasColumnName("kyc_notes");

        builder.Property(c => c.IsPep).HasColumnName("is_pep");
        builder.Property(c => c.AmlRiskLevel).HasColumnName("aml_risk_level").IsRequired();
        builder.Property(c => c.CreditBalance).HasColumnName("credit_balance").HasPrecision(18, 4);
        builder.Property(c => c.PortalUserId).HasColumnName("portal_user_id");
        builder.Property(c => c.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Property(c => c.CreatedBy).HasColumnName("created_by");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
    }
}

internal sealed class CustomerDocumentConfiguration : IEntityTypeConfiguration<CustomerDocument>
{
    public void Configure(EntityTypeBuilder<CustomerDocument> builder)
    {
        builder.ToTable("customer_documents");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(d => d.DocCategory).HasColumnName("doc_category").IsRequired();
        builder.Property(d => d.FileHash).HasColumnName("file_hash").HasMaxLength(100).IsRequired();
        builder.Property(d => d.StorageUri).HasColumnName("storage_uri").IsRequired();
        builder.Property(d => d.MimeType).HasColumnName("mime_type").HasMaxLength(60).IsRequired();
        builder.Property(d => d.SizeBytes).HasColumnName("size_bytes");
        builder.Property(d => d.OriginalName).HasColumnName("original_name").HasMaxLength(255);
        builder.Property(d => d.IsCurrent).HasColumnName("is_current");
        builder.Property(d => d.UploadedBy).HasColumnName("uploaded_by");
        builder.Property(d => d.UploadedAt).HasColumnName("uploaded_at");
        builder.Property(d => d.ReplacedAt).HasColumnName("replaced_at");
    }
}

internal sealed class KycVerificationConfiguration : IEntityTypeConfiguration<KycVerification>
{
    public void Configure(EntityTypeBuilder<KycVerification> builder)
    {
        builder.ToTable("kyc_verifications");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id).HasColumnName("id");
        builder.Property(v => v.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(v => v.CheckType).HasColumnName("check_type").IsRequired();
        builder.Property(v => v.Provider).HasColumnName("provider").HasMaxLength(60).IsRequired();
        builder.Property(v => v.Result).HasColumnName("result").IsRequired();
        builder.Property(v => v.Score).HasColumnName("score").HasPrecision(5, 2);
        builder.Property(v => v.ExternalReference).HasColumnName("external_reference").HasMaxLength(100);
        builder.Property(v => v.RawResponseJson).HasColumnName("raw_response").HasColumnType("jsonb");
        builder.Property(v => v.ErrorMessage).HasColumnName("error_message");
        builder.Property(v => v.RequestedAt).HasColumnName("requested_at");
        builder.Property(v => v.CompletedAt).HasColumnName("completed_at");
        builder.Property(v => v.RequestedBy).HasColumnName("requested_by");
    }
}

internal sealed class ConsentTextConfiguration : IEntityTypeConfiguration<ConsentText>
{
    public void Configure(EntityTypeBuilder<ConsentText> builder)
    {
        builder.ToTable("consent_texts");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.Purpose).HasColumnName("purpose").IsRequired();
        builder.Property(t => t.Version).HasColumnName("version").HasMaxLength(30).IsRequired();
        builder.Property(t => t.Body).HasColumnName("body").IsRequired();
        builder.Property(t => t.Sha256).HasColumnName("sha256").HasColumnType("char(64)").IsRequired();
        builder.Property(t => t.IsCurrent).HasColumnName("is_current");
        builder.Property(t => t.PublishedAt).HasColumnName("published_at");
    }
}

internal sealed class CustomerConsentConfiguration : IEntityTypeConfiguration<CustomerConsent>
{
    public void Configure(EntityTypeBuilder<CustomerConsent> builder)
    {
        builder.ToTable("customer_consents");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(c => c.ConsentTextId).HasColumnName("consent_text_id").IsRequired();
        builder.Property(c => c.Purpose).HasColumnName("purpose").IsRequired();
        builder.Property(c => c.GrantedAt).HasColumnName("granted_at");
        builder.Property(c => c.GrantedIp).HasColumnName("granted_ip");
        builder.Property(c => c.UserAgent).HasColumnName("user_agent").HasMaxLength(512);
        builder.Property(c => c.Channel).HasColumnName("channel").HasMaxLength(30).IsRequired();
        builder.Property(c => c.RevokedAt).HasColumnName("revoked_at");
        builder.Property(c => c.RevokedIp).HasColumnName("revoked_ip");

        builder.HasOne(c => c.ConsentText)
            .WithMany()
            .HasForeignKey(c => c.ConsentTextId);
    }
}

internal sealed class OtpCodeConfiguration : IEntityTypeConfiguration<OtpCode>
{
    public void Configure(EntityTypeBuilder<OtpCode> builder)
    {
        builder.ToTable("otp_verifications");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(o => o.Purpose).HasColumnName("purpose").IsRequired();
        builder.Property(o => o.ReferenceId).HasColumnName("reference_id");
        builder.Property(o => o.Channel).HasColumnName("channel").IsRequired();
        builder.Property(o => o.Target).HasColumnName("target").HasMaxLength(150).IsRequired();
        builder.Property(o => o.OtpHash).HasColumnName("otp_hash").HasMaxLength(255).IsRequired().IsAuditSensitive();
        builder.Property(o => o.ExpiresAt).HasColumnName("expires_at");
        builder.Property(o => o.ConsumedAt).HasColumnName("verified_at");
        builder.Property(o => o.Attempts).HasColumnName("attempts").HasColumnType("smallint");
        builder.Property(o => o.MaxAttempts).HasColumnName("max_attempts").HasColumnType("smallint");
        builder.Property(o => o.InvalidatedAt).HasColumnName("invalidated_at");
        builder.Property(o => o.RequesterIp).HasColumnName("requester_ip");
        builder.Property(o => o.CreatedAt).HasColumnName("created_at");
    }
}

internal sealed class CustomerBankAccountConfiguration : IEntityTypeConfiguration<CustomerBankAccount>
{
    public void Configure(EntityTypeBuilder<CustomerBankAccount> builder)
    {
        builder.ToTable("customer_bank_accounts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(a => a.BankName).HasColumnName("bank_name").HasMaxLength(100).IsRequired();
        builder.Property(a => a.BankCode).HasColumnName("bank_code").HasMaxLength(10);
        builder.Property(a => a.AccountType).HasColumnName("account_type").IsRequired();
        builder.Property(a => a.AccountNumberMasked).HasColumnName("account_number_masked").HasMaxLength(30);
        builder.Property(a => a.AccountNumberEncrypted).HasColumnName("account_number_encrypted").IsAuditSensitive();
        builder.Property(a => a.AccountNumberHash).HasColumnName("account_number_hash").HasMaxLength(64).IsAuditSensitive();
        builder.Property(a => a.Last4).HasColumnName("last4").HasColumnType("char(4)").IsRequired();
        builder.Property(a => a.PhoneWallet).HasColumnName("phone_wallet").HasMaxLength(20);
        builder.Property(a => a.HolderName).HasColumnName("holder_name").HasMaxLength(200);
        builder.Property(a => a.GatewayToken).HasColumnName("gateway_token").HasMaxLength(255).IsAuditSensitive();
        builder.Property(a => a.IsVerified).HasColumnName("is_verified");
        builder.Property(a => a.VerifiedBy).HasColumnName("verified_by");
        builder.Property(a => a.VerifiedAt).HasColumnName("verified_at");
        builder.Property(a => a.VerificationMethod).HasColumnName("verification_method").HasMaxLength(30);
        builder.Property(a => a.VerificationNotes).HasColumnName("verification_notes");
        builder.Property(a => a.IsPrimary).HasColumnName("is_primary");
        builder.Property(a => a.Status).HasColumnName("status").IsRequired();
        builder.Property(a => a.DisbursementEnabledFrom).HasColumnName("disbursement_enabled_from");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.DeactivatedAt).HasColumnName("deactivated_at");
    }
}
