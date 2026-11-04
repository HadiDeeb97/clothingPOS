namespace ClothingStore.Core.Entities;

/// <summary>
/// One till's own settings (its printers, cash drawer, customer screen, text size, label layout, panel and column sizes),
/// kept in the shop database so they come back if the PC loses its copy (a restart during a save, a reinstall).
/// </summary>
public class TillSettings : Entity
{
    /// <summary>The PC's ID (the same one the license uses).</summary>
    public string TillId { get; set; } = "";

    /// <summary>The PC's name, for people to read.</summary>
    public string? TillName { get; set; }

    /// <summary>The settings as the till keeps them (JSON).</summary>
    public string Json { get; set; } = "{}";

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
