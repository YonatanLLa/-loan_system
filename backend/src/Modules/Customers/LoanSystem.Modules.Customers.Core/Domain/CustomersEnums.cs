namespace LoanSystem.Modules.Customers.Core.Domain;

// Exact member names mirror PostgreSQL enum labels mapped with NpgsqlNullNameTranslator.

internal enum PersonType
{
    NATURAL,
    JURIDICA
}

internal enum DocumentType
{
    DNI,
    CE,
    RUC,
    PASAPORTE
}

internal enum KycStatus
{
    PENDING,
    VERIFIED,
    REJECTED,
    EXPIRED
}

internal enum AmlRiskLevel
{
    LOW,
    MEDIUM,
    HIGH
}

internal enum KycCheckType
{
    IDENTITY_REGISTRY,
    BIOMETRIC_LIVENESS,
    FACE_MATCH,
    PEP_SCREENING,
    AML_SANCTIONS
}

internal enum KycCheckResult
{
    PENDING,
    PASS,
    FAIL,
    REVIEW,
    ERROR
}

internal enum DocumentCategory
{
    DNI_FRONTAL,
    DNI_REVERSO,
    SELFIE,
    BOLETA_PAGO,
    RECIBO_SERVICIOS,
    ESTADO_CUENTA_BANCO,
    FICHA_RUC,
    OTRO
}

internal enum ConsentPurpose
{
    DATA_PROCESSING,
    CREDIT_BUREAU_QUERY,
    MARKETING
}

internal enum OtpPurpose
{
    LOGIN,
    CONTACT_VERIFICATION,
    CONTRACT_SIGNATURE,
    ACCOUNT_CHANGE
}

internal enum OtpChannel
{
    EMAIL,
    SMS,
    WHATSAPP,
    PUSH
}

internal enum BankAccountType
{
    CUENTA_CORRIENTE,
    CUENTA_AHORRO,
    TARJETA_DEBITO,
    BILLETERA_DIGITAL
}

internal enum BankAccountStatus
{
    ACTIVE,
    INACTIVE,
    PENDING_VERIFICATION
}
