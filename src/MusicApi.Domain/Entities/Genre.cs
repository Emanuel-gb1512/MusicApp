namespace MusicApi.Domain.Entities;

/// <summary>
/// Un género musical (tabla Genres).
/// </summary>
public class Genre
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
