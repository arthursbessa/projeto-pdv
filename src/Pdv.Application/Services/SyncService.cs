using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pdv.Application.Abstractions;
using Pdv.Application.Domain;

namespace Pdv.Application.Services;

public sealed class SyncService
{
    private static readonly TimeSpan MissingSessionRetryDelay = TimeSpan.FromSeconds(5);
    private readonly IOutboxRepository _outboxRepository;
    private readonly ISalesApiClient _salesApiClient;
    private readonly IRefundsApiClient _refundsApiClient;
    private readonly ISalesRepository _salesRepository;
    private readonly ICashRegisterApiClient _cashRegisterApiClient;
    private readonly ICashRegisterRepository _cashRegisterRepository;
    private readonly IErrorLogger _errorLogger;

    public SyncService(
        IOutboxRepository outboxRepository,
        ISalesApiClient salesApiClient,
        IRefundsApiClient refundsApiClient,
        ISalesRepository salesRepository,
        ICashRegisterApiClient cashRegisterApiClient,
        ICashRegisterRepository cashRegisterRepository,
        IErrorLogger errorLogger)
    {
        _outboxRepository = outboxRepository;
        _salesApiClient = salesApiClient;
        _refundsApiClient = refundsApiClient;
        _salesRepository = salesRepository;
        _cashRegisterApiClient = cashRegisterApiClient;
        _cashRegisterRepository = cashRegisterRepository;
        _errorLogger = errorLogger;
    }

    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var events = await _outboxRepository.GetPendingEventsAsync(now, 50, cancellationToken);
        var sent = 0;

        foreach (var outboxEvent in OrderByDispatchPriority(events))
        {
            try
            {
                await DispatchEventAsync(outboxEvent, cancellationToken);
                await _outboxRepository.MarkAsSentAsync(outboxEvent.Id, DateTimeOffset.UtcNow, cancellationToken);
                sent++;
            }
            catch (RemoteSessionNotLinkedException ex)
            {
                var nextRetry = DateTimeOffset.UtcNow.Add(MissingSessionRetryDelay);
                await _outboxRepository.MarkForRetryAsync(outboxEvent.Id, outboxEvent.Attempts, nextRetry, ex.Message, cancellationToken);
            }
            catch (Exception ex)
            {
                _errorLogger.LogError($"Falha ao integrar evento '{outboxEvent.Type}' (id: {outboxEvent.Id})", ex);
                var attempts = outboxEvent.Attempts + 1;
                var nextRetry = DateTimeOffset.UtcNow.Add(SyncBackoffPolicy.NextDelay(attempts));
                await _outboxRepository.MarkForRetryAsync(outboxEvent.Id, attempts, nextRetry, ex.Message, cancellationToken);
            }
        }

        return sent;
    }

    private static IEnumerable<OutboxEvent> OrderByDispatchPriority(IEnumerable<OutboxEvent> events)
    {
        return events
            .OrderBy(GetDispatchPriority)
            .ThenBy(e => e.CreatedAt);
    }

    private static int GetDispatchPriority(OutboxEvent outboxEvent)
    {
        return outboxEvent.Type switch
        {
            "CashRegisterOpened" => 0,
            "SaleCreated" => 1,
            "SaleRefundCreated" => 1,
            "CashWithdrawalCreated" => 1,
            "CashRegisterClosed" => 2,
            _ => 3
        };
    }

    private async Task DispatchEventAsync(OutboxEvent outboxEvent, CancellationToken cancellationToken)
    {
        switch (outboxEvent.Type)
        {
            case "SaleCreated":
                await SendSaleAsync(outboxEvent.PayloadJson, cancellationToken);
                break;
            case "SaleRefundCreated":
                {
                    var payloadNode = JsonNode.Parse(outboxEvent.PayloadJson)?.AsObject() ?? [];
                    payloadNode.Remove("local_refund_id");
                    await _refundsApiClient.RegisterRefundAsync(payloadNode.ToJsonString(), cancellationToken);
                }
                break;
            case "CashWithdrawalCreated":
                using (var document = JsonDocument.Parse(outboxEvent.PayloadJson))
                {
                    var root = document.RootElement;
                    var localSessionId = root.GetProperty("session_id").GetString() ?? string.Empty;
                    var sessionId = await ResolveRemoteSessionIdOrThrowAsync(localSessionId, cancellationToken);
                    var operatorId = root.GetProperty("operator_id").GetString() ?? string.Empty;
                    var amount = root.GetProperty("amount").GetDecimal();
                    var description = root.GetProperty("description").GetString() ?? string.Empty;
                    await _cashRegisterApiClient.RegisterWithdrawalAsync(sessionId, operatorId, amount, description, cancellationToken);
                }
                break;
            case "CashRegisterOpened":
            case "CashRegisterClosed":
                using (var document = JsonDocument.Parse(outboxEvent.PayloadJson))
                {
                    var root = document.RootElement;
                    var action = root.GetProperty("action").GetString() ?? string.Empty;
                    var operatorId = root.GetProperty("operator_id").GetString() ?? string.Empty;
                    var amount = root.GetProperty("amount").GetDecimal();
                    var datetime = root.GetProperty("datetime").GetDateTimeOffset();

                    if (action == "open")
                    {
                        var remoteSessionId = await _cashRegisterApiClient.OpenAsync(operatorId, amount, datetime, cancellationToken);
                        var localSessionId = root.TryGetProperty("local_session_id", out var localSessionElement)
                            ? localSessionElement.GetString()
                            : null;

                        if (!string.IsNullOrWhiteSpace(localSessionId))
                        {
                            await _cashRegisterRepository.SaveRemoteSessionIdAsync(localSessionId!, remoteSessionId, cancellationToken);
                        }
                    }
                    else if (action == "close")
                    {
                        var localSessionId = root.GetProperty("session_id").GetString() ?? string.Empty;
                        var sessionId = await ResolveRemoteSessionIdOrThrowAsync(localSessionId, cancellationToken);
                        var notes = root.TryGetProperty("notes", out var notesElement)
                            ? notesElement.GetString()
                            : null;
                        await _cashRegisterApiClient.CloseAsync(sessionId, operatorId, amount, datetime, notes, cancellationToken);
                    }
                }
                break;
            default:
                throw new InvalidOperationException($"Tipo de evento de integracao desconhecido: {outboxEvent.Type}");
        }
    }

    private async Task SendSaleAsync(string payloadJson, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(payloadJson);
        var payloadNode = JsonNode.Parse(payloadJson)?.AsObject() ?? [];
        Guid? localSaleId = null;
        if (payloadNode["local_sale_id"] is JsonValue localSaleValue
            && Guid.TryParse(localSaleValue.ToString(), out var parsedLocalSaleId))
        {
            localSaleId = parsedLocalSaleId;
        }
        payloadNode.Remove("local_sale_id");

        SaleSyncResult result;
        if (!document.RootElement.TryGetProperty("session_id", out var sessionIdElement))
        {
            result = await _salesApiClient.SendSaleAsync(payloadNode.ToJsonString(), cancellationToken);
        }
        else
        {
            var localSessionId = sessionIdElement.GetString() ?? string.Empty;
            var remoteSessionId = await ResolveRemoteSessionIdOrThrowAsync(localSessionId, cancellationToken);
            payloadNode["session_id"] = remoteSessionId;
            result = await _salesApiClient.SendSaleAsync(payloadNode.ToJsonString(), cancellationToken);
        }

        if (localSaleId.HasValue && !string.IsNullOrWhiteSpace(result.RemoteSaleId))
        {
            await _salesRepository.SaveRemoteSaleReferenceAsync(localSaleId.Value, result.RemoteSaleId, result.SaleNumber, cancellationToken);
        }
    }

    private async Task<string> ResolveRemoteSessionIdOrThrowAsync(string localSessionId, CancellationToken cancellationToken)
    {
        var remoteSessionId = await _cashRegisterRepository.GetRemoteSessionIdAsync(localSessionId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(remoteSessionId))
        {
            return remoteSessionId;
        }

        throw new RemoteSessionNotLinkedException(localSessionId);
    }
}

public sealed class RemoteSessionNotLinkedException : InvalidOperationException
{
    public RemoteSessionNotLinkedException(string localSessionId)
        : base($"Sessao remota ainda nao vinculada para a sessao local '{localSessionId}'.")
    {
    }
}
