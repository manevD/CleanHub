using CleanHub.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CleanHub.Infrastructure.Data
{
    public class ApplicationDbMartiContext : IdentityDbContext
    {
        public virtual DbSet<PartneriTest> PartneriTest { get; set; }
        public virtual DbSet<DokumentiTest> DokumentiTest { get; set; }

        public ApplicationDbMartiContext()
        {
          
        }

        public ApplicationDbMartiContext(DbContextOptions<ApplicationDbMartiContext> options)
            : base(options)
        {
        }

      
       // protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
       //  => optionsBuilder.UseSqlServer(
       //"Server=92.51.163.105;Port=3306;Database=higiena;User ID=higiena;Password=Sru?28p0");
    }
}