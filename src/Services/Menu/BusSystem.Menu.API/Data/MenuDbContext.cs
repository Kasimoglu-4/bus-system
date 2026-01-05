using BusSystem.Menu.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BusSystem.Menu.API.Data;

public class MenuDbContext : DbContext
{
    public MenuDbContext(DbContextOptions<MenuDbContext> options) : base(options)
    {
    }

    public DbSet<Category> Categories { get; set; }
    public DbSet<MenuItem> MenuItems { get; set; }

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

        // Configure Category entity
        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Category");
            entity.HasKey(e => e.CategoryId);
            entity.HasIndex(e => new { e.BusId, e.Name });
            
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100);
                
            entity.Property(e => e.BusId)
                .IsRequired();

            // One-to-many relationship with MenuItems
            entity.HasMany(e => e.MenuItems)
                .WithOne(e => e.Category)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure MenuItem entity
        modelBuilder.Entity<MenuItem>(entity =>
        {
            entity.ToTable("MenuItem");
            entity.HasKey(e => e.MenuItemId);
            entity.HasIndex(e => e.CategoryId);
            
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);
                
            entity.Property(e => e.Description)
                .HasMaxLength(500);
                
            entity.Property(e => e.Image)
                .HasMaxLength(500);
                
            entity.Property(e => e.Price)
                .IsRequired()
                .HasColumnType("decimal(10,2)");
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
