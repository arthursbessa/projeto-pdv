namespace Pdv.Ui.Services;

public interface IErrorFileLogger
{
    void LogError(string context, Exception exception);
}
