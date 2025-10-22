using Microsoft.EntityFrameworkCore;
using AlmazayaTravel.Models;

namespace AlmazayaTravel.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<TripPackage> TripPackages { get; set; }
        public DbSet<Booking> Bookings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TripPackage>()
                .Property(p => p.PriceBeforeDiscount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<TripPackage>()
                .Property(p => p.PriceAfterDiscount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Booking>()
                .Property(b => b.AmountPaid)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.TripPackage)
                .WithMany(tp => tp.Bookings)
                .HasForeignKey(b => b.TripPackageId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
