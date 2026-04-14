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
    private bool _isEditMode;
    private string _customerId = Guid.NewGuid().ToString();
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
    public bool IsEditMode
    {
        get => _isEditMode;
        private set
        {
            if (SetField(ref _isEditMode, value))
            {
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(SaveButtonText));
                OnPropertyChanged(nameof(ShowDeleteButton));
            }
        }
    }

    public string WindowTitle => IsEditMode ? "Editar cliente" : "Novo cliente";
    public string SaveButtonText => IsEditMode ? "Salvar cliente" : "Criar cliente";
    public bool ShowDeleteButton => IsEditMode;

    public string CustomerId
    {
        get => _customerId;
        private set => SetField(ref _customerId, value);
    }

    public string StatusMessage { get => _statusMessage; private set => SetField(ref _statusMessage, value); }
    public Brush StatusBrush { get => _statusBrush; private set => SetField(ref _statusBrush, value); }
    public string Name { get => _name; set => SetField(ref _name, value); }
    public string Cpf { get => _cpf; set => SetField(ref _cpf, value); }
    public string Phone { get => _phone; set => SetField(ref _phone, value); }
    public string Email { get => _email; set => SetField(ref _email, value); }
    public string Address { get => _address; set => SetField(ref _address, value); }
    public string Notes { get => _notes; set => SetField(ref _notes, value); }

    public void New()
    {
        CustomerId = Guid.NewGuid().ToString();
        Name = string.Empty;
        Cpf = string.Empty;
        Phone = string.Empty;
        Email = string.Empty;
        Address = string.Empty;
        Notes = string.Empty;
        IsEditMode = false;
        StatusMessage = "Preencha os dados do cliente.";
        StatusBrush = Brushes.DimGray;
    }

    public void LoadExisting(CustomerRecord customer)
    {
        CustomerId = customer.Id;
        Name = customer.Name;
        Cpf = customer.Cpf;
        Phone = customer.Phone;
        Email = customer.Email;
        Address = customer.Address;
        Notes = customer.Notes;
        IsEditMode = true;
        StatusMessage = "Edite os dados do cliente.";
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
                Id = CustomerId,
                Name = Name.Trim(),
                Cpf = Cpf.Trim(),
                Phone = Phone.Trim(),
                Email = Email.Trim(),
                Address = Address.Trim(),
                Notes = Notes.Trim(),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            if (IsEditMode)
            {
                CreatedCustomer = await _customersApiClient.UpdateCustomerAsync(customer);
            }
            else
            {
                CreatedCustomer = await _customersApiClient.CreateCustomerAsync(customer);
            }

            await _customerRepository.UpsertAsync(CreatedCustomer);
            StatusMessage = IsEditMode ? "Cliente atualizado com sucesso." : "Cliente cadastrado com sucesso.";
            StatusBrush = Brushes.SeaGreen;
            IsEditMode = true;
            return true;
        }
        catch (Exception ex)
        {
            _errorLogger.LogError("Falha ao cadastrar cliente no PDV", ex);
            StatusMessage = "Erro ao salvar cliente. Tente novamente.";
            StatusBrush = Brushes.Firebrick;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> DeleteAsync()
    {
        if (!IsEditMode || IsBusy)
        {
            return false;
        }

        IsBusy = true;
        try
        {
            await _customersApiClient.DeleteCustomerAsync(CustomerId);
            await _customerRepository.DeleteAsync(CustomerId);
            StatusMessage = "Cliente excluido.";
            StatusBrush = Brushes.SeaGreen;
            return true;
        }
        catch (Exception ex)
        {
            _errorLogger.LogError("Falha ao excluir cliente no PDV", ex);
            StatusMessage = "Erro ao excluir cliente.";
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
