using System.IO;

namespace Pdv.Application.Configuration;

public sealed class AppStoragePaths
{
    private const string AppFolderName = "PDV-Client";

    public AppStoragePaths()
    {
        InstallRoot = Path.GetFullPath(AppContext.BaseDirectory);

        RuntimeRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            AppFolderName);

        DataDirectory = Path.Combine(RuntimeRoot, "data");
        AssetsDirectory = Path.Combine(DataDirectory, "assets");
        LogsDirectory = Path.Combine(InstallRoot, "logs");

        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(AssetsDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }

    public string InstallRoot { get; }
    public string RuntimeRoot { get; }
    public string DataDirectory { get; }
    public string AssetsDirectory { get; }
    public string LogsDirectory { get; }

    public string ResolveDatabasePath(string configuredRelativePath)
    {
        if (string.IsNullOrWhiteSpace(configuredRelativePath))
        {
            return Path.Combine(DataDirectory, "pdv-local.db");
        }

        if (Path.IsPathRooted(configuredRelativePath))
        {
            return configuredRelativePath;
        }

        return Path.GetFullPath(Path.Combine(RuntimeRoot, configuredRelativePath));
    }
}
