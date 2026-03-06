using System.Linq;
using System.Text.Json;
using Pdv.Application.Abstractions;
using Pdv.Application.Domain;

namespace Pdv.Application.Services;

public sealed class SyncService
{
    private readonly IOutboxRepository _outboxRepository;
    private readonly ISalesApiClient _salesApiClient;
    private readonly ICashRegisterApiClient _cashRegisterApiClient;
    private readonly ICashRegisterRepository _cashRegisterRepository;
    private readonly IErrorLogger _errorLogger;

    public SyncService(
        IOutboxRepository outboxRepository,
        ISalesApiClient salesApiClient,
        ICashRegisterApiClient cashRegisterApiClient,
        ICashRegisterRepository cashRegisterRepository,
        IErrorLogger errorLogger)
    {
        _outboxRepository = outboxRepository;
        _salesApiClient = salesApiClient;
        _cashRegisterApiClient = cashRegisterApiClient;
        _cashRegisterRepository = cashRegisterRepository;
        _errorLogger = errorLogger;
    }

    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var events = await _outboxRepository.GetPendingEventsAsync(now, 50, cancellationToken);
        var sent = 0;

        foreach (var outboxEvent in events)
        {
            try
            {
                await DispatchEventAsync(outboxEvent, cancellationToken);
                await _outboxRepository.MarkAsSentAsync(outboxEvent.Id, DateTimeOffset.UtcNow, cancellationToken);
                sent++;
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

    private async Task DispatchEventAsync(OutboxEvent outboxEvent, CancellationToken cancellationToken)
    {
        switch (outboxEvent.Type)
        {
            case "SaleCreated":
                await SendSaleAsync(outboxEvent.PayloadJson, cancellationToken);
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
                throw new InvalidOperationException($"Tipo de evento de integração desconhecido: {outboxEvent.Type}");
        }
    }


    private async Task SendSaleAsync(string payloadJson, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(payloadJson);
        var root = document.RootElement;

        if (!root.TryGetProperty("session_id", out var sessionIdElement))
        {
            await _salesApiClient.SendSaleAsync(payloadJson, cancellationToken);
            return;
        }

        var localSessionId = sessionIdElement.GetString() ?? string.Empty;
        var remoteSessionId = await ResolveRemoteSessionIdOrThrowAsync(localSessionId, cancellationToken);

        var payload = JsonSerializer.Serialize(new
        {
            sale_id = root.GetProperty("sale_id").GetGuid(),
            created_at = root.GetProperty("created_at").GetDateTimeOffset(),
            session_id = remoteSessionId,
            payment_method = root.GetProperty("payment_method").GetString(),
            payment_method_code = root.GetProperty("payment_method_code").GetString(),
            payment_method_id = root.GetProperty("payment_method_id").GetInt32(),
            total_cents = root.GetProperty("total_cents").GetInt32(),
            operator_id = root.TryGetProperty("operator_id", out var operatorElement) ? operatorElement.GetString() : null,
            items = root.GetProperty("items").EnumerateArray().Select(item => new
            {
                product_id = item.GetProperty("product_id").GetString(),
                quantity = item.GetProperty("quantity").GetInt32(),
                price_cents = item.GetProperty("price_cents").GetInt32(),
                subtotal_cents = item.GetProperty("subtotal_cents").GetInt32()
            })
        });

        await _salesApiClient.SendSaleAsync(payload, cancellationToken);
    }

    private async Task<string> ResolveRemoteSessionIdOrThrowAsync(string localSessionId, CancellationToken cancellationToken)
    {
        var remoteSessionId = await _cashRegisterRepository.GetRemoteSessionIdAsync(localSessionId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(remoteSessionId))
        {
            return remoteSessionId;
        }

        throw new InvalidOperationException($"Sessão remota ainda não vinculada para a sessão local '{localSessionId}'.");
    }

}
