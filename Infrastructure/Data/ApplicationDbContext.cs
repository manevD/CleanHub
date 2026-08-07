using CleanHub.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SpecialInvoice = CleanHub.Entities.SpecialInvoice;

namespace CleanHub.Infrastructure.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public virtual DbSet<Product> Products { get; set; }
        public virtual DbSet<BuildingProduct> BuildingProducts { get; set; }

        public virtual DbSet<Building> Buildings { get; set; }
        public virtual DbSet<Activity> Activity { get; set; }

        public virtual DbSet<Bank> Banks { get; set; }

        public virtual DbSet<Document> Documents { get; set; }

        public virtual DbSet<Book> Books { get; set; }

        public virtual DbSet<BookFinancial> BookFinancials { get; set; }

        public virtual DbSet<SpecialInvoice> SpecialInvoices { get; set; }

        public virtual DbSet<Customer> Customers { get; set; }

        // private readonly IEncryptionProvider _provider;
        //private readonly string _key = "09e88d4fd3c6fa2f9b05a05f166809b7";
        public ApplicationDbContext()
        {
        }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
            ChangeTracker.LazyLoadingEnabled = true;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            base.OnModelCreating(modelBuilder);
            // modelBuilder.UseEncryption(this._provider);
            // modelBuilder.Entity<CompanyConfig>().HasNoKey();
            // Add configurations for your entities, including primary keys
            modelBuilder.Entity<IdentityUserLogin<string>>().HasKey(l => l.UserId);
            //modelBuilder.Entity<Activity>().ToTable("Activity", t => t.ExcludeFromMigrations());
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
         => optionsBuilder.UseSqlServer("Data Source=SQL6032.site4now.net;Initial Catalog=db_aae56c_2025martitest;User Id=db_aae56c_2025martitest_admin;Password=Hallo123!");
        //protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        //    => optionsBuilder.UseSqlServer(
        //"Server=.\\SQLEXPRESS;Database=db_aae56c_2025martiNew;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;");
    }
}