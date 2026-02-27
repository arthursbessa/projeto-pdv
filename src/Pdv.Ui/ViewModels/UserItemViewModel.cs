namespace Pdv.Ui.ViewModels;

public sealed class UserItemViewModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
}
