using MusicApi.Application.Auth;
using MusicApi.Domain.Enums;

namespace MusicApi.Application.Interfaces;

/// <summary>
/// Inicia sesión (o crea la cuenta) a partir de una identidad ya verificada por un proveedor externo.
/// Sirve para Google y, más adelante, para Apple.
/// </summary>
public interface IExternalLoginService
{
    Task<ExternalLoginResult> LoginAsync(
        ExternalProvider provider,
        ExternalIdentity identity,
        DeviceInfo device,
        ClientInfo client,
        CancellationToken cancellationToken = default);
}