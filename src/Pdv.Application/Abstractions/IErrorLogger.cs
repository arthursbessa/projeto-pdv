namespace Pdv.Application.Abstractions;

public interface IErrorLogger
{
    void LogError(string context, Exception exception);
}
