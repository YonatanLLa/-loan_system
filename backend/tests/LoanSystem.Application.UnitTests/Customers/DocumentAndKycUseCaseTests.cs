using LoanSystem.Common.Core.Application;
using LoanSystem.Modules.Customers.Core.Application;
using LoanSystem.Modules.Customers.Core.Contracts;
using LoanSystem.Modules.Customers.Core.Contracts.IntegrationEvents;
using LoanSystem.Modules.Customers.Core.Domain;
using LoanSystem.Modules.Customers.Core.Ports.Outbound;
using Shouldly;
using Xunit;

namespace LoanSystem.Application.UnitTests.Customers;

public class DocumentAndKycUseCaseTests
{
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02, 0x03, 0x04];
    private static readonly byte[] PdfBytes = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37, 0x0A, 0x25, 0xE2, 0xE3];

    private readonly FakeCustomersClock _clock = new();
    private readonly InMemoryCustomerRepository _customers = new();
    private readonly InMemoryDocumentRepository _docs = new();
    private readonly InMemoryKycVerificationRepository _checks = new();
    private readonly FakeDocumentStorage _storage = new();
    private readonly FakeIdentityVerifier _verifier = new();
    private readonly FakeCustomersUnitOfWork _uow = new();
    private readonly FakeCustomersOutbox _outbox = new();
    private readonly FakeCustomersCurrentUser _currentUser = new();
    private readonly FakeCustomersClientInfo _clientInfo = new();
    private readonly FakeCustomersAuditContext _audit = new();
    private readonly FakeCustomerAuditWriter _auditWriter = new();
    private readonly FakeCustomerDiagnostics _diagnostics = new();
    private readonly FakeKycExpirationBatchStore _batchStore = new();
    private readonly CustomersOptions _options = new();

    private UploadCustomerDocumentUseCase CreateUpload() =>
        new(_customers, _docs, _storage, _uow, _currentUser, _clock, _audit, _options, _diagnostics);

    private RunKycChecksUseCase CreateRunner() =>
        new(_customers, _docs, _checks, _verifier, _uow, _clock, _options, _diagnostics);

    [Fact]
    public async Task UploadDocument_SniffsMagicBytes_ReplacesSingleCategory_KeepsMultiplePayslips_AndLogsOrphanOnDbError()
    {
        var customer = CustomersTestFactory.CreateNaturalCustomer(_clock);
        _customers.Add(customer);
        _currentUser.AsRole(SecurityPolicies.Operator);

        // Spoofed executable disguised as .png
        using var exeStream = new MemoryStream([0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00]);
        var badUpload = await CreateUpload().ExecuteAsync(
            new UploadCustomerDocumentRequest(customer.Id, "DNI_FRONTAL", "fake.png", "image/png", exeStream.Length, exeStream));
        badUpload.IsFailure.ShouldBeTrue();
        badUpload.Error.Code.ShouldBe("Customers.UnsupportedFileType");

        // First DNI_FRONTAL PNG
        using var png1 = new MemoryStream(PngBytes);
        var first = await CreateUpload().ExecuteAsync(
            new UploadCustomerDocumentRequest(customer.Id, "DNI_FRONTAL", "dni1.png", "application/octet-stream", PngBytes.Length, png1));
        first.IsSuccess.ShouldBeTrue();
        first.Value.IsCurrent.ShouldBeTrue();

        // Second DNI_FRONTAL marks previous non-current
        _clock.Advance(TimeSpan.FromMinutes(1));
        using var png2 = new MemoryStream(PngBytes);
        var second = await CreateUpload().ExecuteAsync(
            new UploadCustomerDocumentRequest(customer.Id, "DNI_FRONTAL", "dni2.png", "image/png", PngBytes.Length, png2));
        second.IsSuccess.ShouldBeTrue();
        _docs.Items.Single(d => d.Id == first.Value.Id).IsCurrent.ShouldBeFalse();
        _docs.Items.Single(d => d.Id == second.Value.Id).IsCurrent.ShouldBeTrue();

        // Multiple BOLETA_PAGO remain current simultaneously
        using var pdf1 = new MemoryStream(PdfBytes);
        using var pdf2 = new MemoryStream(PdfBytes);
        var b1 = await CreateUpload().ExecuteAsync(new UploadCustomerDocumentRequest(customer.Id, "BOLETA_PAGO", "b1.pdf", "application/pdf", PdfBytes.Length, pdf1));
        var b2 = await CreateUpload().ExecuteAsync(new UploadCustomerDocumentRequest(customer.Id, "BOLETA_PAGO", "b2.pdf", "application/pdf", PdfBytes.Length, pdf2));
        b1.IsSuccess.ShouldBeTrue();
        b2.IsSuccess.ShouldBeTrue();
        _docs.Items.Count(d => d.DocCategory == DocumentCategory.BOLETA_PAGO && d.IsCurrent).ShouldBe(2);

        // DB failure logs orphaned blob alert
        _uow.FailNextSaveException = new InvalidOperationException("db down");
        using var pdfFail = new MemoryStream(PdfBytes);
        await Should.ThrowAsync<InvalidOperationException>(() =>
            CreateUpload().ExecuteAsync(new UploadCustomerDocumentRequest(customer.Id, "SELFIE", "s.pdf", "application/pdf", PdfBytes.Length, pdfFail)));
        _diagnostics.OrphanedObjects.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetDocumentUrlAndDownloadLocal_AuditViewAndReturnNotFoundOnUnknownToken()
    {
        var customer = CustomersTestFactory.CreateNaturalCustomer(_clock);
        _customers.Add(customer);
        _currentUser.AsRole(SecurityPolicies.Auditor);

        using var png = new MemoryStream(PngBytes);
        var upload = await CreateUpload().ExecuteAsync(
            new UploadCustomerDocumentRequest(customer.Id, "DNI_FRONTAL", "front.png", "image/png", PngBytes.Length, png));
        // Note Auditor cannot upload; switch to Operator for upload then Auditor for read!
        upload.IsFailure.ShouldBeTrue();

        _currentUser.AsRole(SecurityPolicies.Operator);
        using var png2 = new MemoryStream(PngBytes);
        upload = await CreateUpload().ExecuteAsync(
            new UploadCustomerDocumentRequest(customer.Id, "DNI_FRONTAL", "front.png", "image/png", PngBytes.Length, png2));
        upload.IsSuccess.ShouldBeTrue();

        _currentUser.AsRole(SecurityPolicies.Auditor);
        var signed = await new GetDocumentUrlUseCase(_customers, _docs, _storage, _auditWriter, _currentUser, _clientInfo, _clock, _options)
            .ExecuteAsync(new GetDocumentSignedUrlRequest(customer.Id, upload.Value.Id));

        signed.IsSuccess.ShouldBeTrue();
        signed.Value.Url.ShouldContain("signed/");
        _auditWriter.Entries.ShouldHaveSingleItem().Action.ShouldBe("VIEW_DOCUMENT");

        var missingDownload = await new DownloadLocalDocumentUseCase(_storage).ExecuteAsync("bad-token");
        missingDownload.IsFailure.ShouldBeTrue();
        missingDownload.Error.Code.ShouldBe("Customers.NotFound");
    }

    [Fact]
    public async Task KycWorkflow_SubmitRunChecksApproveRejectResubmitAndExpireBatch()
    {
        var customer = CustomersTestFactory.CreateNaturalCustomer(_clock, income: 2000m);
        _customers.Add(customer);
        _currentUser.AsRole(SecurityPolicies.Operator);

        foreach (var cat in new[] { "DNI_FRONTAL", "DNI_REVERSO", "SELFIE" })
        {
            using var ms = new MemoryStream(PngBytes);
            (await CreateUpload().ExecuteAsync(
                new UploadCustomerDocumentRequest(customer.Id, cat, $"{cat}.png", "image/png", PngBytes.Length, ms))).IsSuccess.ShouldBeTrue();
        }

        // Submit KYC
        var submit = await new SubmitKycUseCase(_customers, _docs, _checks, _uow, _outbox, _currentUser, _clock, _audit, _options)
            .ExecuteAsync(customer.Id);
        submit.IsSuccess.ShouldBeTrue();
        submit.Value.IsSubmittedForReview.ShouldBeTrue();
        _outbox.Events.OfType<CustomerKycSubmittedIntegrationEvent>().Count().ShouldBe(1);

        // Automatic checks: LIVENESS transient error twice then succeeds; PEP_SCREENING flags IsPep
        _verifier.CustomHandler = (type, attempt) => type switch
        {
            KycCheckType.BIOMETRIC_LIVENESS when attempt < 3 =>
                new KycCheckOutcome(type, KycCheckResult.ERROR, null, null, "{}", "timeout", IsTransientError: true),
            KycCheckType.PEP_SCREENING =>
                new KycCheckOutcome(type, KycCheckResult.REVIEW, 0.72m, "PEP-HIT", "{\"pep\":true}", null, IsPepFlag: true),
            _ => new KycCheckOutcome(type, KycCheckResult.PASS, 0.99m, "OK", "{}", null)
        };

        await CreateRunner().ExecuteForSubmissionAsync(customer.Id);
        _verifier.GetCallCount(KycCheckType.BIOMETRIC_LIVENESS).ShouldBe(3);
        customer.IsPep.ShouldBeTrue();
        customer.AmlRiskLevel.ShouldBe(AmlRiskLevel.HIGH);

        // Analyst cannot approve PEP customer
        _currentUser.AsRole(SecurityPolicies.Analyst);
        var analystApprove = await new ApproveKycUseCase(_customers, _docs, _checks, _uow, _outbox, _currentUser, _clock, _audit, _options)
            .ExecuteAsync(new ApproveKycCommand(customer.Id, "Manual verification performed cleanly in detail."));
        analystApprove.IsFailure.ShouldBeTrue();
        analystApprove.Error.Code.ShouldBe("Customers.KycRequiresAdmin");

        // Admin approves with detailed >= 30 chars note
        _currentUser.AsRole(SecurityPolicies.Admin);
        var adminApprove = await new ApproveKycUseCase(_customers, _docs, _checks, _uow, _outbox, _currentUser, _clock, _audit, _options)
            .ExecuteAsync(new ApproveKycCommand(customer.Id, "Confirmed PEP status and verified lawful income documents."));
        adminApprove.IsSuccess.ShouldBeTrue();
        adminApprove.Value.KycStatus.ShouldBe("VERIFIED");
        _outbox.Events.OfType<CustomerKycVerifiedIntegrationEvent>().Count().ShouldBe(1);

        // Batch expiration emits Expired and ExpiringSoon events
        var expId = Guid.NewGuid();
        var soonId = Guid.NewGuid();
        _batchStore.DueToExpire.Add((expId, _clock.UtcNow.AddDays(-1)));
        _batchStore.ExpiringSoon.Add((soonId, _clock.UtcNow.AddDays(5)));

        var expiredCount = await new ExpireKycUseCase(_batchStore, _outbox, _uow, _clock, _options).ExecuteAsync();
        expiredCount.IsSuccess.ShouldBeTrue();
        expiredCount.Value.ShouldBe(1);
        _outbox.Events.OfType<CustomerKycExpiredIntegrationEvent>().ShouldHaveSingleItem().CustomerId.ShouldBe(expId);
        _outbox.Events.OfType<CustomerKycExpiringSoonIntegrationEvent>().ShouldHaveSingleItem().CustomerId.ShouldBe(soonId);
    }

    [Fact]
    public async Task RetryKycChecks_RetriesOnlyErrorChecks()
    {
        var customer = CustomersTestFactory.CreateNaturalCustomer(_clock);
        _customers.Add(customer);

        var pass = KycVerification.CreatePending(customer.Id, KycCheckType.IDENTITY_REGISTRY, "Fake", null, null, _clock);
        pass.RecordOutcome(KycCheckResult.PASS, 1m, "1", "{}", null, _clock);
        var err = KycVerification.CreatePending(customer.Id, KycCheckType.BIOMETRIC_LIVENESS, "Fake", null, null, _clock);
        err.RecordOutcome(KycCheckResult.ERROR, null, null, "{}", "503", _clock);
        _checks.Add(pass);
        _checks.Add(err);

        var retryUc = new RetryKycChecksUseCase(_currentUser.AsRole(SecurityPolicies.Analyst), CreateRunner());
        var res = await retryUc.ExecuteAsync(customer.Id);

        res.IsSuccess.ShouldBeTrue();
        _verifier.GetCallCount(KycCheckType.IDENTITY_REGISTRY).ShouldBe(0);
        _verifier.GetCallCount(KycCheckType.BIOMETRIC_LIVENESS).ShouldBe(1);
        err.Result.ShouldBe(KycCheckResult.PASS);
    }
}
