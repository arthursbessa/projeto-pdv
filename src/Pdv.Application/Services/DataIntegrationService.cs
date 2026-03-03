using Pdv.Application.Abstractions;

namespace Pdv.Application.Services;

public sealed class DataIntegrationService
{
    private readonly SyncService _syncService;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IUsersApiClient _usersApiClient;
    private readonly IUserRepository _userRepository;
    private readonly IStoreSettingsApiClient _storeSettingsApiClient;
    private readonly IStoreSettingsRepository _storeSettingsRepository;

    public DataIntegrationService(
        SyncService syncService,
        IOutboxRepository outboxRepository,
        IUsersApiClient usersApiClient,
        IUserRepository userRepository,
        IStoreSettingsApiClient storeSettingsApiClient,
        IStoreSettingsRepository storeSettingsRepository)
    {
        _syncService = syncService;
        _outboxRepository = outboxRepository;
        _usersApiClient = usersApiClient;
        _userRepository = userRepository;
        _storeSettingsApiClient = storeSettingsApiClient;
        _storeSettingsRepository = storeSettingsRepository;
    }

    public async Task<(int SentSales, int PendingSales, int SyncedUsers, bool StoreSettingsSynced)> IntegrateAllAsync(CancellationToken cancellationToken = default)
    {
        var sent = await _syncService.RunOnceAsync(cancellationToken);
        var pending = await _outboxRepository.GetPendingCountAsync(cancellationToken);

        var remoteUsers = await _usersApiClient.GetUsersAsync(cancellationToken);
        await _userRepository.UpsertSyncedUsersAsync(remoteUsers, cancellationToken);

        var settings = await _storeSettingsApiClient.GetSettingsAsync(cancellationToken);
        var syncedSettings = false;
        if (settings is not null)
        {
            await _storeSettingsRepository.UpsertAsync(settings, cancellationToken);
            syncedSettings = true;
        }

        return (sent, pending, remoteUsers.Count, syncedSettings);
    }
}
