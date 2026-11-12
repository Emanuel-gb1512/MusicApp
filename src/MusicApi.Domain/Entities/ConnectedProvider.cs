namespace MusicApi.Domain.Entities;

/// <summary>
/// Un servicio que TONYX conoce (Spotify, YouTube, Jamendo, Google...)
/// y lo que ese servicio permite oficialmente (tabla ConnectedProviders).
/// </summary>
public class ConnectedProvider
{
    public short Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>1 = Música, 2 = Identidad (solo inicio de sesión).</summary>
    public byte Kind { get; set; }

    public bool RequiresOAuth { get; set; }
    public bool SupportsSearch { get; set; }
    public bool SupportsLibrary { get; set; }
    public bool SupportsPlaylists { get; set; }
    public bool SupportsPlaybackControl { get; set; }
    public bool SupportsDownloads { get; set; }
    public bool SupportsOpenInApp { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
