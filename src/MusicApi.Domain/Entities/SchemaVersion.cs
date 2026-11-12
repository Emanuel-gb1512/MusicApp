namespace MusicApi.Domain.Entities;

/// <summary>
/// Un script SQL ya aplicado a la base de datos (tabla SchemaVersions).
/// </summary>
public class SchemaVersion
{
    public int ScriptNumber { get; set; }
    public string ScriptName { get; set; } = string.Empty;
    public DateTime AppliedAtUtc { get; set; }
    public string AppliedBy { get; set; } = string.Empty;
}
