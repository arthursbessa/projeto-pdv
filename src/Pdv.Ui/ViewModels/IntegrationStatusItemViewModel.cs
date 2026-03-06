using System.Windows.Media;

namespace Pdv.Ui.ViewModels;

public sealed class IntegrationStatusItemViewModel
{
    public required string Name { get; init; }
    public required bool IsIntegrated { get; init; }
    public required int PendingCount { get; init; }
    public string StatusText => IsIntegrated ? "Integrado" : $"Pendente ({PendingCount})";
    public Brush StatusColor => IsIntegrated
        ? new SolidColorBrush(Color.FromRgb(22, 163, 74))
        : new SolidColorBrush(Color.FromRgb(220, 38, 38));
}
