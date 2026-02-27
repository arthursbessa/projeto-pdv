using System.Windows;
using Pdv.Application.Domain;
using Pdv.Ui.Formatting;

namespace Pdv.Ui.Views;

public partial class CashReportWindow : Window
{
    public CashReportWindow(IReadOnlyList<CashReportEntry> entries)
    {
        InitializeComponent();

        var lines = entries
            .Select(x => new CashReportLine(
                x.CreatedAt,
                x.Type,
                x.ReferenceId,
                MoneyFormatter.FormatFromCents(x.AmountCents)))
            .ToList();

        var totalBalanceCents = entries.Sum(x => x.AmountCents);
        DataContext = new CashReportViewData(lines, $"Balanço total do caixa: {MoneyFormatter.FormatFromCents(totalBalanceCents)}");
    }

    public sealed record CashReportLine(
        DateTimeOffset CreatedAt,
        string Type,
        string Reference,
        string AmountFormatted);

    public sealed record CashReportViewData(
        IReadOnlyList<CashReportLine> Entries,
        string TotalBalanceFormatted);
}
