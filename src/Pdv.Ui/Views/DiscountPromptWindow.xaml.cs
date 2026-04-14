using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace Pdv.Ui.Views;

public partial class DiscountPromptWindow : Window, INotifyPropertyChanged
{
    private decimal _discountPercent;
    private string _previewText = string.Empty;

    public DiscountPromptWindow(string? currentDiscount)
    {
        InitializeComponent();
        DataContext = this;
        if (decimal.TryParse(
                (currentDiscount ?? "0").Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var value))
        {
            _discountPercent = Math.Clamp(value, 0m, 100m);
        }

        Loaded += (_, _) =>
        {
            DiscountTextBox.Text = _discountPercent.ToString("0.##");
            DiscountTextBox.SelectAll();
            DiscountTextBox.Focus();
            UpdatePreview();
        };
    }

    public decimal DiscountPercent => _discountPercent;

    public string PreviewText
    {
        get => _previewText;
        private set => SetField(ref _previewText, value);
    }

    private void DiscountTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (decimal.TryParse(
                DiscountTextBox.Text.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var value))
        {
            _discountPercent = Math.Clamp(value, 0m, 100m);
        }
        else
        {
            _discountPercent = 0m;
        }

        UpdatePreview();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Confirm_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Cancel_Click(sender, e);
            e.Handled = true;
        }
    }

    private void UpdatePreview()
    {
        PreviewText = _discountPercent <= 0
            ? "Sem desconto aplicado."
            : $"Desconto atual: {_discountPercent:0.##}%";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
