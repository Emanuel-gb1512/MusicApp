using MusicApi.Application.Providers;
using MusicApi.Domain.Enums;

namespace MusicApi.Application.Music;

/// <summary>Parámetros de una búsqueda: texto, proveedor opcional y máximo de pistas.</summary>
public sealed record MusicSearchRequest(string? Q, string? Provider, int Limit);

/// <summary>Un proveedor que falló durante una búsqueda en varios.</summary>
public sealed record ProviderSearchError(ExternalProvider Provider, string Code);

/// <summary>Resultados por proveedor, más los proveedores que fallaron.</summary>
public sealed record MusicSearchResponse(
    string Query,
    IReadOnlyList<ProviderSearchResult> Results,
    IReadOnlyList<ProviderSearchError> Errors);

/// <summary>Motivos por los que una operación musical puede no completarse.</summary>
public enum MusicOutcome
{
    Ok = 0,
    ProviderNotFound,
    ProviderNotSupported,
    ProviderNotConfigured,
    TrackNotFound,
    ProviderUnavailable
}

/// <summary>Resultado de una operación musical: un valor, o el motivo del fallo.</summary>
public sealed record MusicResult<T>(T? Value, MusicOutcome Outcome) where T : class
{
    public bool Succeeded => Outcome == MusicOutcome.Ok;

    public static MusicResult<T> Success(T value) => new(value, MusicOutcome.Ok);

    public static MusicResult<T> Failure(MusicOutcome outcome) => new(null, outcome);
}