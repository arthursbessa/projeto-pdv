using Pdv.Application.Abstractions;
using Pdv.Application.Domain;
using Pdv.Application.Services;
using Xunit;

namespace Pdv.Tests;

public sealed class SyncServiceTests
{
    [Fact]
    public async Task RunOnceAsync_ShouldPrioritizeCashRegisterOpenedBeforeDependentEvents()
    {
        var localSessionId = "local-1";
        var localSaleId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var openEvent = new OutboxEvent
        {
            Id = Guid.NewGuid(),
            Type = "CashRegisterOpened",
            PayloadJson = $$"""
            {
              "action": "open",
              "operator_id": "op-1",
              "amount": 50.0,
              "datetime": "{{now:O}}",
              "local_session_id": "{{localSessionId}}"
            }
            """,
            Attempts = 0,
            CreatedAt = now.AddSeconds(1)
        };

        var saleEvent = new OutboxEvent
        {
            Id = Guid.NewGuid(),
            Type = "SaleCreated",
            PayloadJson = $$"""
            {
              "local_sale_id": "{{localSaleId}}",
              "session_id": "{{localSessionId}}",
              "payment_method": "cash",
              "items": []
            }
            """,
            Attempts = 0,
            CreatedAt = now
        };

        var outbox = new FakeOutboxRepository([saleEvent, openEvent]);
        var salesApi = new FakeSalesApiClient();
        var refundsApi = new FakeRefundsApiClient();
        var salesRepository = new FakeSalesRepository();
        var cashRegisterApi = new FakeCashRegisterApiClient();
        var cashRegisterRepository = new FakeCashRegisterRepository();
        var logger = new FakeErrorLogger();
        var sut = new SyncService(outbox, salesApi, refundsApi, salesRepository, cashRegisterApi, cashRegisterRepository, logger);

        var sent = await sut.RunOnceAsync();

        Assert.Equal(2, sent);
        Assert.Equal(openEvent.Id, outbox.MarkedAsSent[0]);
        Assert.Equal(saleEvent.Id, outbox.MarkedAsSent[1]);
        Assert.Single(salesApi.Payloads);
        Assert.Contains("remote-1", salesApi.Payloads[0]);
        Assert.Single(salesRepository.SavedReferences);
        Assert.Equal(localSaleId, salesRepository.SavedReferences[0].LocalSaleId);
        Assert.Equal("remote-sale-1", salesRepository.SavedReferences[0].RemoteSaleId);
        Assert.Empty(outbox.RetryCalls);
        Assert.Empty(logger.LoggedErrors);
    }

    [Fact]
    public async Task RunOnceAsync_ShouldRetryWithoutIncrementAttempts_WhenSessionIsNotLinked()
    {
        var localSessionId = "local-1";
        var now = DateTimeOffset.UtcNow;
        var saleEvent = new OutboxEvent
        {
            Id = Guid.NewGuid(),
            Type = "SaleCreated",
            PayloadJson = $$"""
            {
              "local_sale_id": "{{Guid.NewGuid()}}",
              "session_id": "{{localSessionId}}",
              "payment_method": "cash",
              "items": []
            }
            """,
            Attempts = 3,
            CreatedAt = now
        };

        var outbox = new FakeOutboxRepository([saleEvent]);
        var salesApi = new FakeSalesApiClient();
        var refundsApi = new FakeRefundsApiClient();
        var salesRepository = new FakeSalesRepository();
        var cashRegisterApi = new FakeCashRegisterApiClient();
        var cashRegisterRepository = new FakeCashRegisterRepository();
        var logger = new FakeErrorLogger();
        var sut = new SyncService(outbox, salesApi, refundsApi, salesRepository, cashRegisterApi, cashRegisterRepository, logger);

        var sent = await sut.RunOnceAsync();

        Assert.Equal(0, sent);
        Assert.Empty(outbox.MarkedAsSent);
        Assert.Single(outbox.RetryCalls);
        Assert.Equal(3, outbox.RetryCalls[0].Attempts);
        Assert.Contains("Sessao remota ainda nao vinculada", outbox.RetryCalls[0].Error);
        Assert.Empty(logger.LoggedErrors);
    }

    private sealed class FakeOutboxRepository : IOutboxRepository
    {
        private readonly IReadOnlyList<OutboxEvent> _events;

        public FakeOutboxRepository(IReadOnlyList<OutboxEvent> events)
        {
            _events = events;
        }

        public List<Guid> MarkedAsSent { get; } = [];
        public List<(Guid Id, int Attempts, DateTimeOffset NextRetryAt, string Error)> RetryCalls { get; } = [];

        public Task<IReadOnlyList<OutboxEvent>> GetPendingEventsAsync(DateTimeOffset now, int take, CancellationToken cancellationToken = default)
            => Task.FromResult(_events);

        public Task<IReadOnlyDictionary<string, int>> GetPendingCountsByTypeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, int>>(new Dictionary<string, int>());

        public Task MarkAsSentAsync(Guid id, DateTimeOffset sentAt, CancellationToken cancellationToken = default)
        {
            MarkedAsSent.Add(id);
            return Task.CompletedTask;
        }

        public Task MarkForRetryAsync(Guid id, int attempts, DateTimeOffset nextRetryAt, string error, CancellationToken cancellationToken = default)
        {
            RetryCalls.Add((id, attempts, nextRetryAt, error));
            return Task.CompletedTask;
        }

        public Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_events.Count);
    }

    private sealed class FakeSalesApiClient : ISalesApiClient
    {
        public List<string> Payloads { get; } = [];

        public Task<SaleSyncResult> SendSaleAsync(string payloadJson, CancellationToken cancellationToken = default)
        {
            Payloads.Add(payloadJson);
            return Task.FromResult(new SaleSyncResult
            {
                RemoteSaleId = "remote-sale-1",
                SaleNumber = 1042
            });
        }
    }

    private sealed class FakeRefundsApiClient : IRefundsApiClient
    {
        public List<string> Payloads { get; } = [];

        public Task RegisterRefundAsync(string payloadJson, CancellationToken cancellationToken = default)
        {
            Payloads.Add(payloadJson);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSalesRepository : ISalesRepository
    {
        public List<(Guid LocalSaleId, string RemoteSaleId, int? SaleNumber)> SavedReferences { get; } = [];

        public Task SaveSaleWithOutboxAsync(Sale sale, string outboxPayloadJson, string? cashRegisterSessionId = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<SaleHistoryEntry>> GetHistoryAsync(DateTime date, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SaleHistoryEntry>>([]);

        public Task<Sale?> FindByIdAsync(Guid saleId, CancellationToken cancellationToken = default)
            => Task.FromResult<Sale?>(null);

        public Task SaveRemoteSaleReferenceAsync(Guid localSaleId, string remoteSaleId, int? saleNumber, CancellationToken cancellationToken = default)
        {
            SavedReferences.Add((localSaleId, remoteSaleId, saleNumber));
            return Task.CompletedTask;
        }

        public Task SaveRefundAsync(Guid saleId, string reason, IReadOnlyCollection<SaleRefundItem> items, string? operatorId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task SaveRefundWithOutboxAsync(Guid saleId, string reason, IReadOnlyCollection<SaleRefundItem> items, string outboxPayloadJson, string? operatorId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeCashRegisterApiClient : ICashRegisterApiClient
    {
        public Task<string> OpenAsync(string operatorId, decimal openingAmount, DateTimeOffset openedAt, CancellationToken cancellationToken = default)
            => Task.FromResult("remote-1");

        public Task CloseAsync(string sessionId, string operatorId, decimal closingAmount, DateTimeOffset closedAt, string? notes = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RegisterWithdrawalAsync(string sessionId, string operatorId, decimal amount, string description, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeCashRegisterRepository : ICashRegisterRepository
    {
        private readonly Dictionary<string, string> _mapping = [];

        public Task<CashRegisterSession?> GetOpenSessionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<CashRegisterSession?>(null);

        public Task<CashRegisterSession?> GetLastClosedSessionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<CashRegisterSession?>(null);

        public Task<CashRegisterSession> OpenAsync(int openingAmountCents, string userId, DateTimeOffset now, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task CloseAsync(string sessionId, string userId, DateTimeOffset now, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RegisterWithdrawalAsync(string sessionId, int amountCents, string reason, string userId, DateTimeOffset now, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<string?> GetRemoteSessionIdAsync(string localSessionId, CancellationToken cancellationToken = default)
            => Task.FromResult(_mapping.TryGetValue(localSessionId, out var remoteSessionId) ? remoteSessionId : null);

        public Task SaveRemoteSessionIdAsync(string localSessionId, string remoteSessionId, CancellationToken cancellationToken = default)
        {
            _mapping[localSessionId] = remoteSessionId;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SaleSummary>> GetSalesBySessionAsync(string sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SaleSummary>>([]);

        public Task<CashStatusSnapshot> GetCashStatusSnapshotAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
            => Task.FromResult(new CashStatusSnapshot());
    }

    private sealed class FakeErrorLogger : IErrorLogger
    {
        public List<string> LoggedErrors { get; } = [];

        public void LogError(string context, Exception exception)
        {
            LoggedErrors.Add($"{context}: {exception.Message}");
        }
    }
}
