using CleanHub.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.DataEncryption;
using Microsoft.EntityFrameworkCore.DataEncryption.Providers;
using System.Security.Cryptography;
using System.Text;

namespace CleanHub.Infrastructure.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public DbSet<Building> Buildings { get; set; }
        public DbSet<Resident> Residents { get; set; }
        public DbSet<Invoice> Invoices { get; set; }


        private readonly IEncryptionProvider _provider;
        private readonly string _key = "09e88d4fd3c6fa2f9b05a05f166809b7";

        public ApplicationDbContext()
        {
            byte[] keyBytes = new byte[16];
            byte[] iv = new byte[16];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(iv);
            }
            _provider = new AesProvider(keyBytes, iv);
        }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.UseEncryption(this._provider);

            // Add configurations for your entities, including primary keys
            builder.Entity<IdentityUserLogin<string>>().HasKey(l => l.UserId);
            builder.Entity<Resident>()
             .Property(e => e.FirstName)
             .HasConversion(
                 encryptedValue => Encrypt(encryptedValue, _key), // Custom encryption function
                 decryptedValue => Decrypt(decryptedValue, _key)  // Custom decryption function
             );
            builder.Entity<Resident>()
            .Property(e => e.LastName)
            .HasConversion(
                encryptedValue => Encrypt(encryptedValue, _key), // Custom encryption function
                decryptedValue => Decrypt(decryptedValue, _key)  // Custom decryption function
            );
           
        }
      
        static string Encrypt(string data, string key)
        {
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Encoding.UTF8.GetBytes(key);
                aesAlg.IV = new byte[16]; // You might want to generate a random IV for each encryption in a real-world scenario

                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                using (var msEncrypt = new System.IO.MemoryStream())
                {
                    using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (var swEncrypt = new System.IO.StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(data);
                        }
                    }
                    return Convert.ToBase64String(msEncrypt.ToArray());
                }
            }
        }

        static string Decrypt(string encryptedData, string key)
        {
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Encoding.UTF8.GetBytes(key);
                aesAlg.IV = new byte[16]; // You might want to use the same IV that was used for encryption

                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                using (var msDecrypt = new System.IO.MemoryStream(Convert.FromBase64String(encryptedData)))
                {
                    using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (var srDecrypt = new System.IO.StreamReader(csDecrypt))
                        {
                            return srDecrypt.ReadToEnd();
                        }
                    }
                }
            }
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlServer(
        "Server=localhost\\SQLEXPRESS;Database=CleanHub;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;");
    }
}