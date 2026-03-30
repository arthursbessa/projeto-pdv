using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using Pdv.Application.Abstractions;
using Pdv.Application.Domain;
using Pdv.Ui.Services;

namespace Pdv.Ui.ViewModels;

public sealed class CreateCustomerViewModel : INotifyPropertyChanged
{
    private readonly ICustomersApiClient _customersApiClient;
    private readonly ICustomerRepository _customerRepository;
    private readonly IErrorFileLogger _errorLogger;
    private bool _isBusy;
    private string _statusMessage = "Preencha os dados do cliente.";
    private Brush _statusBrush = Brushes.DimGray;
    private string _name = string.Empty;
    private string _cpf = string.Empty;
    private string _phone = string.Empty;
    private string _email = string.Empty;
    private string _address = string.Empty;
    private string _notes = string.Empty;

    public CreateCustomerViewModel(
        ICustomersApiClient customersApiClient,
        ICustomerRepository customerRepository,
        IErrorFileLogger errorLogger)
    {
        _customersApiClient = customersApiClient;
        _customerRepository = customerRepository;
        _errorLogger = errorLogger;
    }

    public CustomerRecord? CreatedCustomer { get; private set; }
    public bool IsBusy { get => _isBusy; private set => SetField(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => SetField(ref _statusMessage, value); }
    public Brush StatusBrush { get => _statusBrush; private set => SetField(ref _statusBrush, value); }
    public string Name { get => _name; set => SetField(ref _name, value); }
    public string Cpf { get => _cpf; set => SetField(ref _cpf, value); }
    public string Phone { get => _phone; set => SetField(ref _phone, value); }
    public string Email { get => _email; set => SetField(ref _email, value); }
    public string Address { get => _address; set => SetField(ref _address, value); }
    public string Notes { get => _notes; set => SetField(ref _notes, value); }

    public async Task<bool> SaveAsync()
    {
        if (IsBusy)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            StatusMessage = "Nome do cliente e obrigatorio.";
            StatusBrush = Brushes.Firebrick;
            return false;
        }

        IsBusy = true;
        StatusBrush = Brushes.DimGray;
        try
        {
            var customer = new CustomerRecord
            {
                Name = Name.Trim(),
                Cpf = Cpf.Trim(),
                Phone = Phone.Trim(),
                Email = Email.Trim(),
                Address = Address.Trim(),
                Notes = Notes.Trim(),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            CreatedCustomer = await _customersApiClient.CreateCustomerAsync(customer);
            await _customerRepository.UpsertAsync(CreatedCustomer);
            StatusMessage = "Cliente cadastrado com sucesso.";
            StatusBrush = Brushes.SeaGreen;
            return true;
        }
        catch (Exception ex)
        {
            _errorLogger.LogError("Falha ao cadastrar cliente no PDV", ex);
            StatusMessage = "Erro ao cadastrar cliente. Tente novamente.";
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
