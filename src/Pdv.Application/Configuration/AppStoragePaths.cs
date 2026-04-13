using System.IO;

namespace Pdv.Application.Configuration;

public sealed class AppStoragePaths
{
    private const string AppFolderName = "PDV-Client";
    private const string UiProjectFolderName = "Pdv.Ui";

    public AppStoragePaths()
    {
        InstallRoot = Path.GetFullPath(AppContext.BaseDirectory);

        RuntimeRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            AppFolderName);

        DataDirectory = Path.Combine(RuntimeRoot, "data");
        AssetsDirectory = Path.Combine(DataDirectory, "assets");
        LogsDirectory = ResolveLogsDirectory();

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

    private string ResolveLogsDirectory()
    {
        var developmentRoot = TryResolveDevelopmentProjectRoot();
        return developmentRoot is null
            ? Path.Combine(InstallRoot, "logs")
            : Path.Combine(developmentRoot, "logs");
    }

    private string? TryResolveDevelopmentProjectRoot()
    {
        var current = new DirectoryInfo(InstallRoot);
        while (current is not null)
        {
            var looksLikeRepoRoot =
                File.Exists(Path.Combine(current.FullName, ".gitignore")) &&
                Directory.Exists(Path.Combine(current.FullName, "src", UiProjectFolderName));

            if (looksLikeRepoRoot)
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }
}
