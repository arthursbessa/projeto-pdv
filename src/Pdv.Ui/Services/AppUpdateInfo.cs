namespace Pdv.Ui.Services;

public sealed record AppUpdateInfo(
    string VersionTag,
    string ReleaseUrl,
    string AssetName,
    string DownloadUrl);
