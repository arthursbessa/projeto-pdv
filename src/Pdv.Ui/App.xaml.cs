using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
            .AddEnvironmentVariables()
            .Build();

        var options = configuration.GetSection("Pdv").Get<PdvOptions>() ?? new PdvOptions();
        var terminalTokenFromEnv = Environment.GetEnvironmentVariable("TOKEN_PDV");
        if (!string.IsNullOrWhiteSpace(terminalTokenFromEnv))
        {
            options.TerminalToken = terminalTokenFromEnv.Trim();
        }
        var fullDbPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, options.DatabaseRelativePath));
        var dbDirectory = Path.GetDirectoryName(fullDbPath) ?? AppContext.BaseDirectory;
        Directory.CreateDirectory(dbDirectory);

        options.DatabaseFullPath = fullDbPath;

        Services = new ServiceCollection()
            .AddPdvInfrastructure(options, fullDbPath)
            .AddSingleton<IErrorFileLogger, ErrorFileLogger>()
            .AddSingleton<SessionContext>()
            .AddTransient<MainViewModel>()
            .AddTransient<LoginViewModel>()
            .AddTransient<MenuViewModel>()
            .BuildServiceProvider();

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
}
