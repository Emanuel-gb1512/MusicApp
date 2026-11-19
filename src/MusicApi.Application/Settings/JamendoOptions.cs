namespace MusicApi.Application.Settings;

/// <summary>
/// Configuración de Jamendo (sección "Jamendo").
/// ClientId se obtiene gratis en devportal.jamendo.com y va en user-secrets.
/// AudioFormat es el formato del streaming: mp31 (96 kbps), mp32 (VBR, buena calidad), ogg o flac.
/// </summary>
public sealed class JamendoOptions
{
    public const string SectionName = "Jamendo";

    private static readonly string[] AllowedFormats = ["mp31", "mp32", "ogg", "flac"];

    public string ClientId { get; set; } = string.Empty;

    public string AudioFormat { get; set; } = "mp32";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId);

    /// <summary>Lanza un error claro si la configuración no es utilizable.</summary>
    public void Validate()
    {
        if (!AllowedFormats.Contains(AudioFormat, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Jamendo:AudioFormat debe ser mp31, mp32, ogg o flac.");
        }
    }
}