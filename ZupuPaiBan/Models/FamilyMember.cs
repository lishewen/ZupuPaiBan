namespace ZupuPaiBan.Models;

public enum Gender
{
    Male,
    Female
}

public class FamilyMember
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Gender Gender { get; set; } = Gender.Male;
    public string BirthDate { get; set; } = string.Empty;
    public string? DeathDate { get; set; }
    public string? SpouseName { get; set; }
    public string? Biography { get; set; }
    public string? PhotoPath { get; set; }
    public int? FatherId { get; set; }
    public int Generation { get; set; } = 1;
    public int BirthOrder { get; set; }

    // Navigation property
    public FamilyMember? Father { get; set; }
    public List<FamilyMember> Children { get; set; } = new();
}
