using Microsoft.EntityFrameworkCore;
using ResidencyRoll.Api.Models;

namespace ResidencyRoll.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Traveller> Travellers => Set<Traveller>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Trip>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(256);
            entity.Property(e => e.CreatedByUserId).HasMaxLength(256);
            entity.Property(e => e.UpdatedByUserId).HasMaxLength(256);
            entity.Property(e => e.Source).HasMaxLength(Trip.MaxSourceLength);
            entity.Property(e => e.BookingRef).HasMaxLength(Trip.MaxBookingRefLength);
            entity.Property(e => e.Notes).HasMaxLength(Trip.MaxNotesLength);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.TravellerId, e.DepartureDateTime });
            entity.HasOne(e => e.Traveller)
                .WithMany()
                .HasForeignKey(e => e.TravellerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Traveller>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OwnerUserId).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(Traveller.MaxNameLength);
            entity.Property(e => e.HomeCountry).HasMaxLength(Traveller.MaxHomeCountryLength);
            entity.Property(e => e.LinkedUserId).HasMaxLength(256);
            entity.HasIndex(e => e.OwnerUserId);
            entity.HasIndex(e => e.LinkedUserId).IsUnique();
            entity.HasIndex(e => new { e.OwnerUserId, e.Name }).IsUnique();
        });

        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).HasMaxLength(256);
            entity.Property(e => e.HomeCountry).HasMaxLength(UserProfile.MaxHomeCountryLength);
        });
    }
}
