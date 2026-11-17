using System.Security.Claims;

namespace MusicApi.Api.Extensions;

/// <summary>
/// Lee del JWT los identificadores del usuario ("sub") y de la sesión ("sid").
/// Los nombres de los claims no se transforman (MapInboundClaims = false).
/// </summary>
public static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal principal) =>
        ParseGuid(principal.FindFirst("sub")?.Value);

    public static Guid? GetSessionId(this ClaimsPrincipal principal) =>
        ParseGuid(principal.FindFirst("sid")?.Value);

    private static Guid? ParseGuid(string? value) =>
        Guid.TryParse(value, out var id) ? id : null;
}