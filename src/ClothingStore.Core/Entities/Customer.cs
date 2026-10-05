namespace ClothingStore.Core.Entities;

public class Customer : Entity
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public DateTime? Birthday { get; set; }
    public string? Notes { get; set; }
    public int LoyaltyPoints { get; set; }
    public decimal StoreCredit { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public int Version { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();

    public override string ToString() =>
        string.IsNullOrWhiteSpace(Phone) ? FullName : $"{FullName} ({Phone})";
}
