using KlawQ.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace KlawQ.Data
{
    public class ApplicationDbContext : IdentityDbContext<IdentityUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Scheduler> Schedulers { get; set; }

        public DbSet<Appointment> Appointments { get; set; }

        // Optional: track simple user profile linked to Identity user
        public DbSet<Users> UserProfiles { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure one-to-one relationship between Appointment and Scheduler
            builder.Entity<Appointment>()
                   .HasOne(a => a.Scheduler)
                   .WithOne(s => s.Appointment)
                   .HasForeignKey<Scheduler>(s => s.AppId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Configure Inspiration_Image to store large base64 strings
            builder.Entity<Appointment>()
                   .Property(a => a.Inspiration_Image)
                   .HasColumnType("nvarchar(max)");

            // If Users.UserID should map to IdentityUser, use a shadow FK or store IdentityUserId string separately.
        }
    }
}