using System.IO;
using System.Reflection;

namespace Pdv.Ui.Services;

public sealed class AppRuntimeInfoService
{
    public AppRuntimeInfoService()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            ExecutablePath = processPath;
        }
        else
        {
            var entryAssemblyName = Assembly.GetEntryAssembly()?.GetName().Name ?? "Pdv.Ui";
            ExecutablePath = Path.Combine(AppContext.BaseDirectory, $"{entryAssemblyName}.exe");
        }

        InstallDirectory = Path.GetDirectoryName(ExecutablePath) ?? AppContext.BaseDirectory;
#if DEBUG
        IsDevelopment = true;
        VersionTag = "DEV";
#else
        IsDevelopment = false;
        VersionTag = ResolveVersionTag();
#endif
        VersionLabel = IsDevelopment ? "Versao DEV" : $"Versao {VersionTag}";
    }

    public bool IsDevelopment { get; }
    public string VersionTag { get; }
    public string VersionLabel { get; }
    public string ExecutablePath { get; }
    public string InstallDirectory { get; }

    private static string ResolveVersionTag()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(AppRuntimeInfoService).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        var normalizedInformationalVersion = NormalizeVersionTag(informationalVersion);
        if (!string.IsNullOrWhiteSpace(normalizedInformationalVersion))
        {
            return normalizedInformationalVersion;
        }

        var assemblyVersion = assembly.GetName().Version;
        if (assemblyVersion is null)
        {
            return "v1.0.0";
        }

        var versionText = assemblyVersion.Build > 0
            ? $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}"
            : $"{assemblyVersion.Major}.{assemblyVersion.Minor}";

        return $"v{versionText}";
    }

    public static string? NormalizeVersionTag(string? rawVersion)
    {
        if (string.IsNullOrWhiteSpace(rawVersion))
        {
            return null;
        }

        var clean = rawVersion.Trim();
        var metadataIndex = clean.IndexOfAny(['+', '-']);
        if (metadataIndex > 0)
        {
            clean = clean[..metadataIndex];
        }

        if (string.IsNullOrWhiteSpace(clean))
        {
            return null;
        }

        return clean.StartsWith("v", StringComparison.OrdinalIgnoreCase)
            ? clean
            : $"v{clean}";
    }
}
