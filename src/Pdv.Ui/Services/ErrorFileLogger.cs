using System.IO;
using System.Text;

namespace Pdv.Ui.Services;

public sealed class ErrorFileLogger : IErrorFileLogger
{
    private readonly string _logDirectory;
    private static readonly object SyncRoot = new();

    public ErrorFileLogger()
    {
        _logDirectory = Path.Combine(ResolveProjectRoot(), "logs");
        Directory.CreateDirectory(_logDirectory);
    }

    private static string ResolveProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PdvDesktop.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return AppContext.BaseDirectory;
    }

    public void LogError(string context, Exception exception)
    {
        var fileName = $"errors-{DateTime.Now:yyyyMMdd}.txt";
        var filePath = Path.Combine(_logDirectory, fileName);

        var builder = new StringBuilder();
        builder.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}");
        builder.AppendLine(exception.ToString());
        builder.AppendLine(new string('-', 100));

        lock (SyncRoot)
        {
            File.AppendAllText(filePath, builder.ToString());
        }
    }
}
