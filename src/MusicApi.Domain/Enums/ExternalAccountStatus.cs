namespace MusicApi.Domain.Enums;

/// <summary>Estado de una cuenta externa (columna ExternalAccounts.Status).</summary>
public enum ExternalAccountStatus : byte
{
    Linked = 1,
    ReauthorizationRequired = 2,
    Unlinked = 3
}