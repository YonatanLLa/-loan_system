using System.Text.Json;
using LoanSystem.Common.Core.Domain.Approvals;

namespace LoanSystem.Common.Core.Contracts.Approvals;

public sealed record ApprovalDecision(Guid ApprovalRequestId, Guid ApproverId, string? Comment);

public sealed record ApprovalOutcome(string ResultEntity, string ResultId);

public sealed record ApprovalUserRef(Guid Id, string FullName, string Email);

public sealed record ApprovalRequestSummary(
    Guid Id,
    string OperationType,
    string TargetEntity,
    Guid TargetId,
    string Status,
    string Reason,
    string? EvidenceRef,
    ApprovalUserRef RequestedBy,
    DateTimeOffset RequestedAt,
    DateTimeOffset ExpiresAt,
    ApprovalUserRef? DecidedBy,
    DateTimeOffset? DecidedAt);

public sealed record ApprovalRequestDto(
    Guid Id,
    string OperationType,
    string TargetEntity,
    Guid TargetId,
    string Status,
    JsonElement Payload,
    JsonElement TargetSnapshot,
    JsonElement? CurrentTargetState,
    string Reason,
    string? EvidenceRef,
    ApprovalUserRef RequestedBy,
    DateTimeOffset RequestedAt,
    DateTimeOffset ExpiresAt,
    ApprovalUserRef? DecidedBy,
    DateTimeOffset? DecidedAt,
    string? DecisionComment);

public sealed record ApprovalRequestHeader(
    Guid Id,
    string OperationType,
    string Module,
    string TargetEntity,
    Guid TargetId,
    ApprovalStatus Status,
    Guid RequestedBy,
    DateTimeOffset ExpiresAt,
    string PayloadJson);

public sealed record ApprovalQuery(
    string? Status = null,
    string? OperationType = null,
    Guid? TargetId = null,
    Guid? RequestedBy = null,
    string? Scope = null,
    int Page = 1,
    int PageSize = 20);

public sealed record ApproveRequest(string? Comment = null, string? MfaCode = null);

public sealed record RejectRequest(string Comment);

public sealed record ApprovalDecisionResponse(Guid Id, string Status, ApprovalOutcome? Result = null);
