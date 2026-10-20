namespace KargoApi.Entities
{
    public class Branch
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string City { get; set; } = null!;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public byte[] RowVersion { get; set; } = null!;
    }
}
