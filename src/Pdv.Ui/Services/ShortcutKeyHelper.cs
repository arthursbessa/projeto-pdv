using System.Windows.Input;

namespace Pdv.Ui.Services;

public static class ShortcutKeyHelper
{
    public static readonly IReadOnlyList<string> AvailableKeys =
    [
        "Enter",
        "Escape",
        "Space",
        "F1",
        "F2",
        "F3",
        "F4",
        "F5",
        "F6",
        "F7",
        "F8",
        "F9",
        "F10",
        "F11",
        "F12"
    ];

    public static string ToDisplayString(Key key)
    {
        return key switch
        {
            Key.Return => "Enter",
            Key.Escape => "Escape",
            Key.Space => "Space",
            _ => key.ToString()
        };
    }

    public static string NormalizeKeyName(string? keyName)
    {
        return (keyName ?? string.Empty).Trim() switch
        {
            "Esc" => "Escape",
            "Return" => "Enter",
            "Spacebar" => "Space",
            var value => value
        };
    }
}
