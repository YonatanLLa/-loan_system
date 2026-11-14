using LoanSystem.Common.Core.Contracts;
using LoanSystem.Common.Core.Ports.Inbound;
using LoanSystem.Modules.Customers.Core.Contracts;

namespace LoanSystem.Modules.Customers.Core.Ports.Inbound;

public interface IRegisterCustomerUseCase : IUseCase<RegisterCustomerRequest, CustomerResponse>;
public interface IGetCustomerUseCase : IUseCase<Guid, CustomerResponse>;
public interface ISearchCustomersUseCase : IUseCase<SearchCustomersRequest, PagedResult<CustomerListItem>>;
public interface IUpdateCustomerUseCase : IUseCase<UpdateCustomerCommand, CustomerResponse>;
public interface IGetCustomerHistoryUseCase : IUseCase<GetCustomerHistoryRequest, PagedResult<CustomerChangeEntry>>;
public interface IGetCustomerOverviewUseCase : IUseCase<Guid, CustomerOverviewResponse>;

public interface IUploadCustomerDocumentUseCase : IUseCase<UploadCustomerDocumentRequest, CustomerDocumentResponse>;
public interface IListCustomerDocumentsUseCase : IUseCase<ListCustomerDocumentsRequest, IReadOnlyList<CustomerDocumentResponse>>;
public interface IGetDocumentUrlUseCase : IUseCase<GetDocumentSignedUrlRequest, DocumentSignedUrlResponse>;

public interface IGetCustomerKycUseCase : IUseCase<Guid, CustomerKycStatusResponse>;
public interface ISubmitKycUseCase : IUseCase<Guid, CustomerKycStatusResponse>;
public interface IApproveKycUseCase : IUseCase<ApproveKycCommand, CustomerKycStatusResponse>;
public interface IRejectKycUseCase : IUseCase<RejectKycCommand, CustomerKycStatusResponse>;
public interface IResubmitKycUseCase : IUseCase<Guid, CustomerKycStatusResponse>;
public interface IRetryKycChecksUseCase : IUseCase<Guid, IReadOnlyList<KycVerificationResponse>>;
public interface IExpireKycUseCase : IEmptyUseCase<int>;

public interface IGetCurrentConsentsUseCase : IEmptyUseCase<IReadOnlyList<CurrentConsentTextResponse>>;
public interface IListCustomerConsentsUseCase : IUseCase<Guid, IReadOnlyList<CustomerConsentResponse>>;
public interface IGrantCustomerConsentUseCase : IUseCase<GrantConsentCommand, CustomerConsentResponse>;
public interface IRevokeCustomerConsentUseCase : IUseCase<RevokeConsentCommand>;

public interface IRegisterBankAccountUseCase : IUseCase<RegisterBankAccountCommand, BankAccountResponse>;
public interface IListBankAccountsUseCase : IUseCase<Guid, IReadOnlyList<BankAccountResponse>>;
public interface IVerifyBankAccountUseCase : IUseCase<VerifyBankAccountCommand, BankAccountResponse>;
public interface IDeactivateBankAccountUseCase : IUseCase<DeactivateBankAccountCommand>;
public interface ISetPrimaryBankAccountUseCase : IUseCase<SetPrimaryBankAccountCommand, SetPrimaryBankAccountResult>;
public interface IConfirmPrimaryBankAccountUseCase : IUseCase<ConfirmPrimaryBankAccountCommand, BankAccountResponse>;

public interface IStartContactVerificationUseCase : IUseCase<StartContactVerificationCommand, OtpIssued>;
public interface IConfirmContactVerificationUseCase : IUseCase<ConfirmContactVerificationCommand>;

public interface IRunKycChecksUseCase
{
    Task ExecuteForSubmissionAsync(Guid customerId, CancellationToken ct = default);
}

public interface IDownloadLocalDocumentUseCase : IUseCase<string, LocalDocumentContent>;
