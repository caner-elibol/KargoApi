using Kargo.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kargo.Infrastructure.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {

        public DbSet<Branch> Branches => Set<Branch>();
        public DbSet<Shipment> Shipments => Set<Shipment>();
        public DbSet<SearchOutbox> SearchOutbox => Set<SearchOutbox>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<Branch>(e =>
            {
                e.ToTable("Branches", "dbo");
                e.Property(x => x.Name).HasMaxLength(100).IsRequired();
                e.Property(x => x.City).HasMaxLength(50).IsRequired();
                e.Property(x => x.IsActive).HasDefaultValue(true);
                e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIME()");
                e.Property(x => x.RowVersion).IsRowVersion();
            });
            b.Entity<Shipment>(e =>
            {
                e.ToTable("Shipments", "dbo");
                e.Property(s => s.TrackingNo).HasConversion(t => t.Value,v => new TrackingNumber(v)).HasMaxLength(20);
                e.Property(x => x.SenderName).HasMaxLength(100);
                e.Property(x => x.ReceiverCity).HasMaxLength(50);
                e.Property(x => x.WeightKg).HasPrecision(6, 2);
                e.Ignore(s => s.DomainEvents);

                e.HasOne(x => x.OriginBranch)
                 .WithMany()
                 .HasForeignKey(x => x.OriginBranchId);

                e.HasIndex(x => x.TrackingNo)
                 .IncludeProperties(x => new { x.Status, x.CreatedAt })
                 .HasDatabaseName("IX_Shipments_TrackingNo_Covering");

                e.HasIndex(x => new { x.ReceiverCity, x.CreatedAt })
                 .IncludeProperties(x => x.TrackingNo)
                 .HasDatabaseName("IX_Shipments_City_CreatedAt");

                e.HasIndex(x => x.CreatedAt)
                 .IncludeProperties(x => new { x.TrackingNo, x.Status })
                 .HasDatabaseName("IX_Shipments_CreatedAt");

                e.HasIndex(x => x.Status)
                 .HasDatabaseName("IX_Shipments_Status");
            });
            b.Entity<SearchOutbox>(e =>
            {
                e.ToTable("SearchOutbox", "dbo");
                e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIME()");
                e.HasIndex(x => x.Id)
                 .HasFilter("[ProcessedAt] IS NULL")
                 .HasDatabaseName("IX_SearchOutbox_Pending");
            });

        }
    }
}
