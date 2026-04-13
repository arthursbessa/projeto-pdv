using Pdv.Application.Domain;

namespace Pdv.Application.Abstractions;

public interface IPdvSettingsRepository
{
    Task<PdvSettings> GetCurrentAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(PdvSettings settings, CancellationToken cancellationToken = default);
}
