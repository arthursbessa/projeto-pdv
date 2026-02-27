using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pdv.Application.Configuration;
using Pdv.Infrastructure.Setup;
using Pdv.Ui.ViewModels;

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
            .Build();

        var options = configuration.GetSection("Pdv").Get<PdvOptions>() ?? new PdvOptions();
        var dbPath = Path.Combine(AppContext.BaseDirectory, "pdv-local.db");

        var services = new ServiceCollection()
            .AddPdvInfrastructure(options, dbPath)
            .AddSingleton<MainViewModel>()
            .BuildServiceProvider();

        Services = services;

        var dbInitializer = Services.GetRequiredService<DatabaseInitializer>();
        await dbInitializer.InitializeAsync();

        var window = new MainWindow
        {
            DataContext = Services.GetRequiredService<MainViewModel>()
        };

        window.Show();
    }
}
