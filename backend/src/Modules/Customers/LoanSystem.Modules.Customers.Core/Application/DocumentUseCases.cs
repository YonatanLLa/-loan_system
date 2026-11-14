using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Customers.Core.Contracts;
using LoanSystem.Modules.Customers.Core.Domain;
using LoanSystem.Modules.Customers.Core.Domain.Validators;
using LoanSystem.Modules.Customers.Core.Ports.Inbound;
using LoanSystem.Modules.Customers.Core.Ports.Outbound;

namespace LoanSystem.Modules.Customers.Core.Application;

internal sealed class UploadCustomerDocumentUseCase : IUploadCustomerDocumentUseCase
{
    private readonly ICustomerRepository _customers;
    private readonly ICustomerDocumentRepository _documents;
    private readonly IDocumentStorage _storage;
    private readonly ICustomersUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IAuditContext _audit;
    private readonly CustomersOptions _options;
    private readonly ICustomerDiagnostics _diagnostics;

    public UploadCustomerDocumentUseCase(
        ICustomerRepository customers,
        ICustomerDocumentRepository documents,
        IDocumentStorage storage,
        ICustomersUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        IAuditContext audit,
        CustomersOptions options,
        ICustomerDiagnostics diagnostics)
    {
        _customers = customers;
        _documents = documents;
        _storage = storage;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
        _audit = audit;
        _options = options;
        _diagnostics = diagnostics;
    }

    public async Task<Result<CustomerDocumentResponse>> ExecuteAsync(
        UploadCustomerDocumentRequest request,
        CancellationToken ct = default)
    {
        if (!CustomerMapper.HasCustomerAccess(
                _currentUser,
                request.CustomerId,
                SecurityPolicies.Admin,
                SecurityPolicies.Analyst,
                SecurityPolicies.Operator))
        {
            return CustomerErrors.NotFound(request.CustomerId);
        }

        if (!Enum.TryParse<DocumentCategory>(request.Category?.Trim(), ignoreCase: true, out var category))
        {
            return CustomerErrors.Validation($"Invalid document category '{request.Category}'.");
        }

        if (request.SizeBytes <= 0)
        {
            return CustomerErrors.Validation("Uploaded file cannot be empty.");
        }

        if (request.SizeBytes > _options.Documents.MaxFileSizeBytes)
        {
            return CustomerErrors.FileTooLarge(_options.Documents.MaxFileSizeMb);
        }

        if (await _customers.FindByIdAsync(request.CustomerId, ct) is null)
        {
            return CustomerErrors.NotFound(request.CustomerId);
        }

        var header = new byte[FileSignatureSniffer.HeaderReadSize];
        var headerRead = await ReadPrefixAsync(request.Content, header, ct);
        var detectedMime = FileSignatureSniffer.DetectAllowedMimeType(
            header.AsSpan(0, headerRead),
            _options.Documents.AllowedTypes);

        if (detectedMime is null)
        {
            return CustomerErrors.UnsupportedFileType;
        }

        var docId = Guid.NewGuid();
        var ext = FileSignatureSniffer.GetExtensionForMimeType(detectedMime);
        var storageKey = $"customers/{request.CustomerId}/{category}/{docId}.{ext}";

        await using var prefixedStream = new PrefixedReadStream(header, headerRead, request.Content);
        StoredObject stored;
        try
        {
            stored = await _storage.PutAsync(storageKey, prefixedStream, detectedMime, ct);
        }
        catch (InvalidOperationException)
        {
            return CustomerErrors.FileTooLarge(_options.Documents.MaxFileSizeMb);
        }

        var doc = CustomerDocument.Create(
            docId,
            request.CustomerId,
            category,
            stored.Sha256,
            stored.StorageUri,
            detectedMime,
            stored.SizeBytes,
            request.OriginalName,
            _currentUser.UserId,
            _clock);

        try
        {
            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);

            if (!CustomerDocument.AllowsMultipleCurrent(category))
            {
                var currentDocs = await _documents.GetCurrentByCategoryAsync(request.CustomerId, category, ct);
                foreach (var previous in currentDocs)
                {
                    previous.MarkReplaced(_clock);
                }
            }

            _audit.SetAction("UPLOAD", "customer_documents");
            _documents.Add(doc);

            await _unitOfWork.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            _diagnostics.LogOrphanedDocumentObject(storageKey, request.CustomerId, ex);
            throw;
        }

        return CustomerMapper.ToDocumentResponse(doc);
    }

    private static async Task<int> ReadPrefixAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), ct);
            if (read == 0)
            {
                break;
            }
            total += read;
        }
        return total;
    }

    private sealed class PrefixedReadStream : Stream
    {
        private readonly byte[] _prefix;
        private readonly int _prefixLength;
        private readonly Stream _inner;
        private int _prefixOffset;

        public PrefixedReadStream(byte[] prefix, int prefixLength, Stream inner)
        {
            _prefix = prefix;
            _prefixLength = prefixLength;
            _inner = inner;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_prefixOffset < _prefixLength)
            {
                var copy = Math.Min(count, _prefixLength - _prefixOffset);
                Buffer.BlockCopy(_prefix, _prefixOffset, buffer, offset, copy);
                _prefixOffset += copy;
                return copy;
            }

            return _inner.Read(buffer, offset, count);
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_prefixOffset < _prefixLength)
            {
                var copy = Math.Min(buffer.Length, _prefixLength - _prefixOffset);
                _prefix.AsMemory(_prefixOffset, copy).CopyTo(buffer);
                _prefixOffset += copy;
                return copy;
            }

            return await _inner.ReadAsync(buffer, cancellationToken);
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

internal sealed class ListCustomerDocumentsUseCase : IListCustomerDocumentsUseCase
{
    private readonly ICustomerRepository _customers;
    private readonly ICustomerDocumentRepository _documents;
    private readonly ICurrentUser _currentUser;

    public ListCustomerDocumentsUseCase(
        ICustomerRepository customers,
        ICustomerDocumentRepository documents,
        ICurrentUser currentUser)
    {
        _customers = customers;
        _documents = documents;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<CustomerDocumentResponse>>> ExecuteAsync(
        ListCustomerDocumentsRequest request,
        CancellationToken ct = default)
    {
        if (!CustomerMapper.HasCustomerAccess(
                _currentUser,
                request.CustomerId,
                SecurityPolicies.Admin,
                SecurityPolicies.Analyst,
                SecurityPolicies.Operator,
                SecurityPolicies.Auditor))
        {
            return CustomerErrors.NotFound(request.CustomerId);
        }

        if (await _customers.FindByIdAsync(request.CustomerId, ct) is null)
        {
            return CustomerErrors.NotFound(request.CustomerId);
        }

        var docs = await _documents.ListByCustomerAsync(request.CustomerId, request.IncludeHistory, ct);
        return docs.Select(CustomerMapper.ToDocumentResponse).ToArray();
    }
}

internal sealed class GetDocumentUrlUseCase : IGetDocumentUrlUseCase
{
    private readonly ICustomerRepository _customers;
    private readonly ICustomerDocumentRepository _documents;
    private readonly IDocumentStorage _storage;
    private readonly ICustomerAuditWriter _auditWriter;
    private readonly ICurrentUser _currentUser;
    private readonly IClientInfoProvider _clientInfo;
    private readonly IClock _clock;
    private readonly CustomersOptions _options;

    public GetDocumentUrlUseCase(
        ICustomerRepository customers,
        ICustomerDocumentRepository documents,
        IDocumentStorage storage,
        ICustomerAuditWriter auditWriter,
        ICurrentUser currentUser,
        IClientInfoProvider clientInfo,
        IClock clock,
        CustomersOptions options)
    {
        _customers = customers;
        _documents = documents;
        _storage = storage;
        _auditWriter = auditWriter;
        _currentUser = currentUser;
        _clientInfo = clientInfo;
        _clock = clock;
        _options = options;
    }

    public async Task<Result<DocumentSignedUrlResponse>> ExecuteAsync(
        GetDocumentSignedUrlRequest request,
        CancellationToken ct = default)
    {
        if (!CustomerMapper.HasCustomerAccess(
                _currentUser,
                request.CustomerId,
                SecurityPolicies.Admin,
                SecurityPolicies.Analyst,
                SecurityPolicies.Operator,
                SecurityPolicies.Auditor))
        {
            return CustomerErrors.NotFound(request.CustomerId);
        }

        if (await _customers.FindByIdAsync(request.CustomerId, ct) is null)
        {
            return CustomerErrors.NotFound(request.CustomerId);
        }

        var doc = await _documents.FindByIdAsync(request.CustomerId, request.DocumentId, ct);
        if (doc is null)
        {
            return CustomerErrors.DocumentNotFound(request.DocumentId);
        }

        var ttl = TimeSpanUtils.Seconds(_options.Documents.SignedUrlTtlSeconds);
        var uri = await _storage.GetSignedReadUrlAsync(doc.StorageUri, ttl, ct);
        var expiresAt = _clock.UtcNow.Add(ttl);

        await _auditWriter.WriteAsync(
            "customer_documents",
            doc.Id.ToString(),
            "VIEW_DOCUMENT",
            _currentUser.UserId,
            _clientInfo.IpAddress,
            _clientInfo.UserAgent,
            $"{{\"customerId\":\"{request.CustomerId}\",\"category\":\"{doc.DocCategory}\"}}",
            ct);

        return new DocumentSignedUrlResponse(uri.ToString(), expiresAt);
    }
}

internal sealed class DownloadLocalDocumentUseCase : IDownloadLocalDocumentUseCase
{
    private readonly ILocalSignedFileReader _reader;

    public DownloadLocalDocumentUseCase(ILocalSignedFileReader reader)
    {
        _reader = reader;
    }

    public async Task<Result<LocalDocumentContent>> ExecuteAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return CustomerErrors.NotFound();
        }

        var file = await _reader.OpenSignedReadAsync(token, ct);
        if (file is null)
        {
            return CustomerErrors.NotFound();
        }

        return new LocalDocumentContent(file.Stream, file.ContentType, file.FileName);
    }
}
