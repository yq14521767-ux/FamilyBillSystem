namespace FamilyBillSystem.Services
{
    public interface ISoftDeletable
    {
        DateTime? DeletedAt { get; set; }
    }
}