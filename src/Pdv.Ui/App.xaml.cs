using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pdv.Application.Abstractions;
using Pdv.Application.Configuration;
using Pdv.Infrastructure.Setup;
using Pdv.Ui.Services;
using Pdv.Ui.ViewModels;
using Pdv.Ui.Views;

namespace Pdv.Ui;

public partial class App : System.Windows.Application
{
    public static ServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        var options = configuration.GetSection("Pdv").Get<PdvOptions>() ?? new PdvOptions();
        var terminalTokenFromEnv = ResolveEnvironmentValue("TOKEN_PDV", "Pdv__TerminalToken");
        options.TerminalToken = string.IsNullOrWhiteSpace(terminalTokenFromEnv)
            ? string.Empty
            : terminalTokenFromEnv.Trim();
        var supabaseAnonKeyFromEnv = ResolveEnvironmentValue(
            "SUPABASE_ANON_KEY",
            "NEXT_PUBLIC_SUPABASE_ANON_KEY",
            "SUPABASE_KEY",
            "SUPABASE_ANON",
            "Pdv__SupabaseAnonKey");
        options.SupabaseAnonKey = string.IsNullOrWhiteSpace(supabaseAnonKeyFromEnv)
            ? string.Empty
            : supabaseAnonKeyFromEnv.Trim();
        var storagePaths = new AppStoragePaths();
        var fullDbPath = storagePaths.ResolveDatabasePath(options.DatabaseRelativePath);
        var dbDirectory = Path.GetDirectoryName(fullDbPath) ?? AppContext.BaseDirectory;
        Directory.CreateDirectory(dbDirectory);

        options.DatabaseFullPath = fullDbPath;

        Services = new ServiceCollection()
            .AddPdvInfrastructure(options, fullDbPath)
            .AddSingleton<IErrorFileLogger, ErrorFileLogger>()
            .AddSingleton<IErrorLogger>(sp => sp.GetRequiredService<IErrorFileLogger>())
            .AddSingleton(storagePaths)
            .AddSingleton<AppRuntimeInfoService>()
            .AddSingleton<GitHubReleaseUpdateService>()
            .AddSingleton<SessionContext>()
            .AddTransient<MainViewModel>()
            .AddTransient<LoginViewModel>()
            .AddTransient<MenuViewModel>()
            .AddTransient<ProductLookupViewModel>()
            .AddTransient<CustomerLookupViewModel>()
            .AddTransient<CreateCustomerViewModel>()
            .AddTransient<SalesHistoryViewModel>()
            .BuildServiceProvider();

        var errorLogger = Services.GetRequiredService<IErrorFileLogger>();
        RegisterGlobalExceptionLogging(errorLogger);

        var dbInitializer = Services.GetRequiredService<DatabaseInitializer>();
        await dbInitializer.InitializeAsync();
        var usersRepository = Services.GetRequiredService<Pdv.Application.Abstractions.IUserRepository>();
        await usersRepository.SeedAdminAsync();

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var login = new LoginWindow { DataContext = Services.GetRequiredService<LoginViewModel>() };
        if (login.ShowDialog() != true)
        {
            Shutdown();
            return;
        }

        var menu = new MenuWindow
        {
            DataContext = Services.GetRequiredService<MenuViewModel>(),
            WindowState = WindowState.Maximized
        };

        MainWindow = menu;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        menu.Show();
    }

    private void RegisterGlobalExceptionLogging(IErrorFileLogger errorLogger)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            errorLogger.LogError("Exceção não tratada na UI", args.Exception);
            args.Handled = true;
            MessageBox.Show("Ocorreu um erro inesperado. Consulte os logs para detalhes.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                errorLogger.LogError("Exceção não tratada no AppDomain", exception);
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            errorLogger.LogError("Exceção de Task não observada", args.Exception);
            args.SetObserved();
        };
    }

    private static string? ResolveEnvironmentValue(params string[] variableNames)
    {
        foreach (var variableName in variableNames)
        {
            var value = Environment.GetEnvironmentVariable(variableName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
