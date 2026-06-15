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

        // Products: nail designs / gallery items
        public DbSet<Products> Products { get; set; }

        // Favorites and Cart
        public DbSet<Favorite> Favorites { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<CalendarConfigure> CalendarConfigures { get; set; }

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

            // Configure Product_Price precision to avoid truncation warnings
            builder.Entity<Products>()
                   .Property(p => p.Product_Price)
                   .HasPrecision(18, 2);

            // Product Type default (Original / PressOn)
            builder.Entity<Products>()
                   .Property(p => p.Product_Type)
                   .HasDefaultValue("Original");

            // Favorites: ensure CreatedAt has default value
            builder.Entity<Favorite>()
                   .Property(f => f.CreatedAt)
                   .HasDefaultValueSql("GETUTCDATE()");

            // Cart relationships: map navigation explicitly to avoid shadow FKs
            builder.Entity<CartItem>()
                   .HasOne(ci => ci.Cart)
                   .WithMany(c => c.Items)
                   .HasForeignKey(ci => ci.CartId)
                   .OnDelete(DeleteBehavior.Cascade);

            // CartItem -> Product relationship
            builder.Entity<CartItem>()
                   .HasOne(ci => ci.Product)
                   .WithMany()
                   .HasForeignKey(ci => ci.ProductID)
                   .OnDelete(DeleteBehavior.Cascade);

            // If Users.UserID should map to IdentityUser, use a shadow FK or store IdentityUserId string separately.
        }
    }
}