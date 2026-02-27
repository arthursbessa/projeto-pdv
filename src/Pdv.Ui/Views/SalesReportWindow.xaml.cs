using System.Windows;
using Pdv.Application.Domain;
using Pdv.Ui.Formatting;

namespace Pdv.Ui.Views;

public partial class SalesReportWindow : Window
{
    public SalesReportWindow(IReadOnlyList<SalesReportEntry> sales)
    {
        InitializeComponent();
        var lines = sales.Select(x => new SalesReportLine(
            x.CreatedAt,
            x.SaleId,
            x.SessionId,
            x.BusinessDate,
            x.OperatorName,
            x.PaymentMethod,
            MoneyFormatter.FormatFromCents(x.TotalCents))).ToList();

        var totalBalanceCents = sales.Sum(x => x.TotalCents);
        DataContext = new SalesReportViewData(lines, $"Balanço total do caixa: {MoneyFormatter.FormatFromCents(totalBalanceCents)}");
    }

    public sealed record SalesReportLine(
        DateTimeOffset CreatedAt,
        string SaleId,
        string SessionId,
        string BusinessDate,
        string OperatorName,
        string PaymentMethod,
        string TotalFormatted);

    public sealed record SalesReportViewData(
        IReadOnlyList<SalesReportLine> Entries,
        string TotalBalanceFormatted);
}
