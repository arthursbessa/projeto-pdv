using System.Text.Json;
using Pdv.Application.Abstractions;
using Pdv.Application.Domain;

namespace Pdv.Application.Services;

public sealed class SyncService
{
    private readonly IOutboxRepository _outboxRepository;
    private readonly ISalesApiClient _salesApiClient;
    private readonly ICashRegisterApiClient _cashRegisterApiClient;
    private readonly IErrorLogger _errorLogger;

    public SyncService(
        IOutboxRepository outboxRepository,
        ISalesApiClient salesApiClient,
        ICashRegisterApiClient cashRegisterApiClient,
        IErrorLogger errorLogger)
    {
        _outboxRepository = outboxRepository;
        _salesApiClient = salesApiClient;
        _cashRegisterApiClient = cashRegisterApiClient;
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
                await _salesApiClient.SendSaleAsync(outboxEvent.PayloadJson, cancellationToken);
                break;
            case "CashWithdrawalCreated":
                using (var document = JsonDocument.Parse(outboxEvent.PayloadJson))
                {
                    var root = document.RootElement;
                    var sessionId = root.GetProperty("session_id").GetString() ?? string.Empty;
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
                        await _cashRegisterApiClient.OpenAsync(operatorId, amount, datetime, cancellationToken);
                    }
                    else if (action == "close")
                    {
                        var sessionId = root.GetProperty("session_id").GetString() ?? string.Empty;
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
}
