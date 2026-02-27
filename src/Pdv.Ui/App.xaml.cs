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
        var fullDbPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, options.DatabaseRelativePath));
        var dbDirectory = Path.GetDirectoryName(fullDbPath) ?? AppContext.BaseDirectory;
        Directory.CreateDirectory(dbDirectory);

        options.DatabaseRelativePath = fullDbPath;

        Services = new ServiceCollection()
            .AddPdvInfrastructure(options, fullDbPath)
            .AddSingleton<MainViewModel>()
            .AddTransient<ProductsViewModel>()
            .BuildServiceProvider();

        var dbInitializer = Services.GetRequiredService<DatabaseInitializer>();
        await dbInitializer.InitializeAsync();

        var window = new MainWindow
        {
            DataContext = Services.GetRequiredService<MainViewModel>()
        };

        window.Show();
    }
}
