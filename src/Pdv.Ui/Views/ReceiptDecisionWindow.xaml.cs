using System.Windows;
using System.Windows.Input;

namespace Pdv.Ui.Views;

public partial class ReceiptDecisionWindow : Window
{
    public ReceiptDecisionWindow()
    {
        InitializeComponent();
    }

    public bool PrintReceipt { get; private set; }

    private void Yes_Click(object sender, RoutedEventArgs e)
    {
        PrintReceipt = true;
        DialogResult = true;
        Close();
    }

    private void No_Click(object sender, RoutedEventArgs e)
    {
        PrintReceipt = false;
        DialogResult = true;
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Yes_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            No_Click(sender, e);
            e.Handled = true;
        }
    }
}
