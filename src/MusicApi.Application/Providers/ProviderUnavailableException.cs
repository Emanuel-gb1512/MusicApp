using MusicApi.Domain.Enums;

namespace MusicApi.Application.Providers;

/// <summary>
/// Un proveedor externo no respondió, tardó demasiado o devolvió un error.
/// El mensaje nunca debe incluir claves ni direcciones con claves.
/// </summary>
public sealed class ProviderUnavailableException : Exception
{
    public ProviderUnavailableException(ExternalProvider provider, string message, Exception? inner = null)
        : base(message, inner)
    {
        Provider = provider;
    }

    public ExternalProvider Provider { get; }
}