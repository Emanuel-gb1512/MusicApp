namespace MusicApi.Domain.Enums;

/// <summary>
/// Proveedores externos. Los valores coinciden con ConnectedProviders.Id (script 003).
/// </summary>
public enum ExternalProvider : short
{
    Spotify = 1,
    YouTube = 2,
    YouTubeMusic = 3,
    Jamendo = 4,
    InternetArchive = 5,
    Google = 6,
    Apple = 7
}