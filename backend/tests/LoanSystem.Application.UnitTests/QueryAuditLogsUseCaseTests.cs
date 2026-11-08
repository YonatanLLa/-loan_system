using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Contracts;
using LoanSystem.Modules.Security.Core.Application;
using LoanSystem.Modules.Security.Core.Contracts;
using LoanSystem.Modules.Security.Core.Ports.Outbound;
using Shouldly;
using Xunit;

namespace LoanSystem.Application.UnitTests;

public class QueryAuditLogsUseCaseTests
{
    private readonly FakeAuditLogReader _reader = new();

    private QueryAuditLogsUseCase CreateUseCase() => new(_reader);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_WhenPageLessThanOne_ReturnsValidationError(int page)
    {
        var useCase = CreateUseCase();
        var query = new AuditLogQuery(Page: page);

        var result = await useCase.ExecuteAsync(query);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Code.ShouldBe("Audit.InvalidPage");
        _reader.QueryCallCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(201)]
    [InlineData(1000)]
    public async Task ExecuteAsync_WhenPageSizeOutOfRange_ReturnsValidationError(int pageSize)
    {
        var useCase = CreateUseCase();
        var query = new AuditLogQuery(PageSize: pageSize);

        var result = await useCase.ExecuteAsync(query);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Code.ShouldBe("Audit.InvalidPageSize");
        _reader.QueryCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenFromDateAfterToDate_ReturnsValidationError()
    {
        var useCase = CreateUseCase();
        var query = new AuditLogQuery(
            From: new DateOnly(2026, 11, 10),
            To: new DateOnly(2026, 11, 5));

        var result = await useCase.ExecuteAsync(query);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Code.ShouldBe("Audit.InvalidDateRange");
        _reader.QueryCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenQueryIsValid_DelegatesToReader()
    {
        var useCase = CreateUseCase();
        var query = new AuditLogQuery(
            EntityName: "users",
            Action: "UPDATE",
            Page: 2,
            PageSize: 20);

        _reader.StubbedResult = new PagedResult<AuditLogItem>(
            Items:
            [
                new AuditLogItem(
                    Id: 1,
                    EntityName: "users",
                    EntityId: "usr-123",
                    Action: "UPDATE",
                    StateBefore: null,
                    StateAfter: null,
                    UserId: Guid.NewGuid(),
                    UserName: "Auditor Test",
                    UserEmail: "auditor@test.com",
                    UserIp: "127.0.0.1",
                    UserAgent: "Mozilla/5.0",
                    CreatedAt: DateTimeOffset.UtcNow)
            ],
            Total: 1,
            Page: 2,
            PageSize: 20);

        var result = await useCase.ExecuteAsync(query);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Total.ShouldBe(1);
        result.Value.Page.ShouldBe(2);
        result.Value.PageSize.ShouldBe(20);
        result.Value.Items.Count.ShouldBe(1);
        result.Value.Items[0].EntityName.ShouldBe("users");
        result.Value.Items[0].Action.ShouldBe("UPDATE");
        _reader.QueryCallCount.ShouldBe(1);
        _reader.LastQuery.ShouldBe(query);
    }

    private sealed class FakeAuditLogReader : IAuditLogReader
    {
        public int QueryCallCount { get; private set; }
        public AuditLogQuery? LastQuery { get; private set; }
        public PagedResult<AuditLogItem> StubbedResult { get; set; } = new([], 0, 1, 50);

        public Task<PagedResult<AuditLogItem>> QueryAsync(AuditLogQuery query, CancellationToken ct = default)
        {
            QueryCallCount++;
            LastQuery = query;
            return Task.FromResult(StubbedResult);
        }
    }
}
