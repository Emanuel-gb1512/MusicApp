namespace MusicApi.Application.Settings;

/// <summary>
/// Configuración del inicio de sesión con Google (sección "Google").
/// ClientIds son los ID de cliente de OAuth cuyos tokens se aceptan (web, Android, iOS).
/// Son identificadores públicos, pero los guardamos en user-secrets para no dejar rastro en el repositorio.
/// El "client secret" de Google NO se usa en el servidor y no debe guardarse en ningún sitio.
/// </summary>
public sealed class GoogleOptions
{
    public const string SectionName = "Google";

    public string[] ClientIds { get; set; } = [];
}