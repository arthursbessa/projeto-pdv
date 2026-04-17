using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Pdv.Application.Abstractions;
using Pdv.Application.Domain;
using Pdv.Application.Utilities;
using Pdv.Ui.Services;

namespace Pdv.Ui.ViewModels;

public sealed class CustomerLookupViewModel : INotifyPropertyChanged
{
    private readonly ICustomersApiClient _customersApiClient;
    private readonly ICustomerRepository _customerRepository;
    private readonly IErrorFileLogger _errorLogger;
    private string _query = string.Empty;
    private CustomerLookupItemViewModel? _selectedCustomer;
    private string _statusMessage = "Carregando clientes...";
    private bool _isBusy;

    public CustomerLookupViewModel(
        ICustomersApiClient customersApiClient,
        ICustomerRepository customerRepository,
        IErrorFileLogger errorLogger)
    {
        _customersApiClient = customersApiClient;
        _customerRepository = customerRepository;
        _errorLogger = errorLogger;
    }

    public ObservableCollection<CustomerLookupItemViewModel> Customers { get; } = [];
    public bool IsBusy { get => _isBusy; private set => SetField(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => SetField(ref _statusMessage, value); }

    public string Query
    {
        get => _query;
        set
        {
            if (SetField(ref _query, value))
            {
                _ = SearchAsync();
            }
        }
    }

    public CustomerLookupItemViewModel? SelectedCustomer
    {
        get => _selectedCustomer;
        set => SetField(ref _selectedCustomer, value);
    }

    public async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            try
            {
                var remoteCustomers = await _customersApiClient.GetCustomersAsync();
                foreach (var customer in remoteCustomers)
                {
                    await _customerRepository.UpsertAsync(customer);
                }

                StatusMessage = $"{remoteCustomers.Count} cliente(s) sincronizado(s).";
            }
            catch (Exception ex)
            {
                _errorLogger.LogError("Falha ao sincronizar clientes na busca manual", ex);
                StatusMessage = "Sem conexao com a API. Exibindo clientes locais.";
            }

            await SearchAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SearchAsync()
    {
        var customers = await _customerRepository.SearchAsync(TextNormalization.TrimToEmpty(Query));
        Customers.Clear();

        foreach (var customer in customers)
        {
            Customers.Add(new CustomerLookupItemViewModel
            {
                Id = customer.Id,
                Name = customer.Name,
                Cpf = TextNormalization.FormatTaxIdPartial(customer.Cpf),
                Phone = customer.Phone,
                Email = customer.Email
            });
        }

        StatusMessage = Customers.Count == 0
            ? "Nenhum cliente encontrado."
            : $"{Customers.Count} cliente(s) disponivel(is).";
    }

    public async Task AddCreatedCustomerAsync(CustomerRecord customer)
    {
        await _customerRepository.UpsertAsync(customer);
        await SearchAsync();
        SelectedCustomer = Customers.FirstOrDefault(x => x.Id == customer.Id);
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
