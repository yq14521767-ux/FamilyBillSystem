namespace FamilyBillSystem.Interfaces
{
    public interface ISoftDeletable
    {
        DateTime? DeletedAt { get; set; }
    }
}