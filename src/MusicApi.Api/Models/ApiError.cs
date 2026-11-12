namespace MusicApi.Api.Models;

/// <summary>
/// Error de la API con un código estable. La app decide qué mostrar según el código,
/// no según el texto del mensaje.
/// </summary>
public sealed record ApiError(string Code, string Message);