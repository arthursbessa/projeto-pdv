using System.Windows;
using Pdv.Application.Domain;
using Pdv.Ui.Formatting;

namespace Pdv.Ui.Views;

public partial class SalesReportWindow : Window
{
    public SalesReportWindow(IReadOnlyList<SalesReportEntry> sales)
    {
        InitializeComponent();
        DataContext = sales.Select(x => new SalesReportLine(
            x.CreatedAt,
            x.SaleId,
            x.SessionId,
            x.BusinessDate,
            x.OperatorName,
            x.PaymentMethod,
            MoneyFormatter.FormatFromCents(x.TotalCents))).ToList();
    }

    public sealed record SalesReportLine(
        DateTimeOffset CreatedAt,
        string SaleId,
        string SessionId,
        string BusinessDate,
        string OperatorName,
        string PaymentMethod,
        string TotalFormatted);
}
