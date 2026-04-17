using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using Pdv.Application.Abstractions;
using Pdv.Application.Domain;
using Pdv.Application.Utilities;
using Pdv.Ui.Services;

namespace Pdv.Ui.ViewModels;

public sealed class CreateSupplierViewModel : INotifyPropertyChanged
{
    private readonly ISuppliersApiClient _suppliersApiClient;
    private readonly IErrorFileLogger _errorLogger;
    private bool _isBusy;
    private string _name = string.Empty;
    private string _cnpj = string.Empty;
    private string _contact = string.Empty;
    private string _phone = string.Empty;
    private string _email = string.Empty;
    private int _avgDeliveryDays;
    private string _notes = string.Empty;
    private string _statusMessage = "Preencha os dados do fornecedor.";
    private Brush _statusBrush = Brushes.DimGray;

    public CreateSupplierViewModel(ISuppliersApiClient suppliersApiClient, IErrorFileLogger errorLogger)
    {
        _suppliersApiClient = suppliersApiClient;
        _errorLogger = errorLogger;
    }

    public LookupOption? CreatedSupplier { get; private set; }

    public bool IsBusy { get => _isBusy; private set => SetField(ref _isBusy, value); }
    public string Name { get => _name; set => SetField(ref _name, value); }
    public string Cnpj { get => _cnpj; set => SetField(ref _cnpj, value); }
    public string Contact { get => _contact; set => SetField(ref _contact, value); }
    public string Phone { get => _phone; set => SetField(ref _phone, value); }
    public string Email { get => _email; set => SetField(ref _email, value); }
    public int AvgDeliveryDays { get => _avgDeliveryDays; set => SetField(ref _avgDeliveryDays, value); }
    public string Notes { get => _notes; set => SetField(ref _notes, value); }
    public string StatusMessage { get => _statusMessage; private set => SetField(ref _statusMessage, value); }
    public Brush StatusBrush { get => _statusBrush; private set => SetField(ref _statusBrush, value); }

    public void New()
    {
        Name = string.Empty;
        Cnpj = string.Empty;
        Contact = string.Empty;
        Phone = string.Empty;
        Email = string.Empty;
        AvgDeliveryDays = 0;
        Notes = string.Empty;
        StatusMessage = "Preencha os dados do fornecedor.";
        StatusBrush = Brushes.DimGray;
    }

    public async Task<bool> SaveAsync()
    {
        if (IsBusy)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            StatusMessage = "Nome do fornecedor e obrigatorio.";
            StatusBrush = Brushes.Firebrick;
            return false;
        }

        IsBusy = true;
        try
        {
            CreatedSupplier = await _suppliersApiClient.CreateAsync(new SupplierCreateRequest
            {
                Name = TextNormalization.TrimToEmpty(Name),
                Cnpj = TextNormalization.FormatTaxId(Cnpj),
                Contact = TextNormalization.TrimToNull(Contact),
                Phone = TextNormalization.TrimToNull(Phone),
                Email = TextNormalization.TrimToNull(Email),
                AvgDeliveryDays = AvgDeliveryDays,
                Notes = TextNormalization.TrimToNull(Notes)
            });
            StatusMessage = "Fornecedor criado com sucesso.";
            StatusBrush = Brushes.SeaGreen;
            return true;
        }
        catch (Exception ex)
        {
            _errorLogger.LogError("Falha ao criar fornecedor no PDV", ex);
            StatusMessage = "Erro ao criar fornecedor.";
            StatusBrush = Brushes.Firebrick;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
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
