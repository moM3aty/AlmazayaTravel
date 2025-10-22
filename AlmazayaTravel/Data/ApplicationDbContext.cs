using Microsoft.EntityFrameworkCore;
using AlmazayaTravel.Models; // Ensure Models namespace is included

namespace AlmazayaTravel.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSet for Trip Packages
        public DbSet<TripPackage> TripPackages { get; set; }

        // DbSet for Bookings
        public DbSet<Booking> Bookings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure decimal precision for TripPackage prices
            modelBuilder.Entity<TripPackage>()
                .Property(p => p.PriceBeforeDiscount)
                .HasPrecision(18, 2); // Defines precision and scale in the database

            modelBuilder.Entity<TripPackage>()
                .Property(p => p.PriceAfterDiscount)
                .HasPrecision(18, 2);

            // Configure decimal precision for Booking amount paid
            modelBuilder.Entity<Booking>()
                .Property(b => b.AmountPaid)
                .HasPrecision(18, 2);

            // Configure the relationship between TripPackage and Booking
            // A TripPackage can have many Bookings
            // A Booking belongs to one TripPackage
            // Configure cascade delete behavior if desired (e.g., prevent deleting a package if it has bookings)
            modelBuilder.Entity<Booking>()
                .HasOne(b => b.TripPackage) // Navigation property in Booking
                .WithMany(tp => tp.Bookings) // Navigation property in TripPackage
                .HasForeignKey(b => b.TripPackageId) // Foreign key property in Booking
                .OnDelete(DeleteBehavior.Restrict); // Prevent deleting TripPackage if Bookings exist
                                                    // Use DeleteBehavior.Cascade if you want bookings deleted when package is deleted
                                                    // Use DeleteBehavior.SetNull if TripPackageId can be nullable

            // --- Seed Data (Optional Example) ---
            // You can add initial data for TripPackages here if needed for testing
            /*
            modelBuilder.Entity<TripPackage>().HasData(
                new TripPackage
                {
                    Id = 1,
                    Name = "Malaysia Marvels",
                    DestinationCountry = "Malaysia",
                    Description = "Explore Kuala Lumpur's skyline, Penang's heritage...",
                    DurationDays = 7,
                    PriceBeforeDiscount = 5000.00m,
                    PriceAfterDiscount = 4500.00m,
                    ImageUrl = "/images/mala.jpg", // Ensure image exists
                    IsActive = true
                },
                new TripPackage
                {
                    Id = 2,
                    Name = "Bali Bliss",
                    DestinationCountry = "Indonesia",
                    Description = "Immerse yourself in Bali's spiritual temples...",
                    DurationDays = 10,
                    PriceBeforeDiscount = 6000.00m,
                    IsActive = true,
                     ImageUrl = "/images/indonesia.jpg", // Ensure image exists
                }
                // Add more seed data...
            );
            */
        }
    }
}