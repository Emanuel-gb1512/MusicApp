namespace MusicApi.Domain.Enums;

/// <summary>Tipo de proveedor (columna ConnectedProviders.Kind).</summary>
public enum ProviderKind : byte
{
    /// <summary>Ofrece música: catálogo, búsqueda, reproducción o descargas.</summary>
    Music = 1,

    /// <summary>Solo sirve para iniciar sesión (Google, Apple).</summary>
    Identity = 2
}