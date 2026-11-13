using LoanSystem.Common.Core.Contracts;
using LoanSystem.Common.Core.Contracts.Approvals;
using LoanSystem.Common.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Contracts;

namespace LoanSystem.Modules.Security.Core.Ports.Inbound;

public interface IQueryApprovalsUseCase : IUseCase<ApprovalQuery, PagedResult<ApprovalRequestSummary>>;

public interface IGetApprovalUseCase : IUseCase<Guid, ApprovalRequestDto>;

public interface IApproveRequestUseCase : IUseCase<ApproveApprovalCommand, ApprovalDecisionResponse>;

public interface IRejectRequestUseCase : IUseCase<RejectApprovalCommand, ApprovalDecisionResponse>;

public interface ICancelRequestUseCase : IUseCase<Guid, ApprovalDecisionResponse>;

public interface IExpireApprovalRequestsUseCase : IEmptyUseCase<int>;

public interface IRequestUserRoleChangeUseCase : IUseCase<RequestUserRoleChangeCommand, ApprovalRequestDto>;
