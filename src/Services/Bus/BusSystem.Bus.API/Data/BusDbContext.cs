using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BusSystem.Bus.API.Data;

public class BusDbContext : DbContext
{
    public BusDbContext(DbContextOptions<BusDbContext> options) : base(options)
    {
    }

    public DbSet<Models.Bus> Buses { get; set; }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Configure all DateTime properties to be stored and retrieved as UTC
        configurationBuilder.Properties<DateTime>()
            .HaveConversion<UtcDateTimeConverter>();
        
        configurationBuilder.Properties<DateTime?>()
            .HaveConversion<UtcNullableDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Models.Bus>(entity =>
        {
            entity.ToTable("Bus");
            entity.HasKey(e => e.BusId);
            entity.HasIndex(e => e.PlateNumber).IsUnique();
            
            entity.Property(e => e.PlateNumber)
                .IsRequired()
                .HasMaxLength(20);
                
            entity.Property(e => e.QRCodeUrl)
                .HasMaxLength(500);
                
            entity.Property(e => e.Description)
                .HasMaxLength(500);
                
            entity.Property(e => e.CreatedDate)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");
                
            entity.Property(e => e.CreatedBy)
                .IsRequired();
        });
    }
}

// Value converter to ensure DateTime values are stored and retrieved as UTC
public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter() : base(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }
}

public class UtcNullableDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    public UtcNullableDateTimeConverter() : base(
        v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v.Value : v.Value.ToUniversalTime()) : v,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v)
    {
    }
}
