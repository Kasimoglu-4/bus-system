using BusSystem.Identity.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BusSystem.Identity.API.Data;

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options)
    {
    }

    public DbSet<AdminUser> AdminUsers { get; set; }

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

        // Configure AdminUser entity
        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.ToTable("AdminUser");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserName).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            
            entity.Property(e => e.UserName)
                .IsRequired()
                .HasMaxLength(50);
                
            entity.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(100);
                
            entity.Property(e => e.Password)
                .IsRequired();
                
            entity.Property(e => e.Role)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("Manager");
                
            entity.Property(e => e.CreatedDate)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");
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
