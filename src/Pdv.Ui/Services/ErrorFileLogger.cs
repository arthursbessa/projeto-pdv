using System.IO;
using System.Text;
using Pdv.Application.Configuration;

namespace Pdv.Ui.Services;

public sealed class ErrorFileLogger : IErrorFileLogger
{
    private readonly string _logDirectory;
    private static readonly object SyncRoot = new();

    public ErrorFileLogger(AppStoragePaths storagePaths)
    {
        _logDirectory = storagePaths.LogsDirectory;
        Directory.CreateDirectory(_logDirectory);
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
