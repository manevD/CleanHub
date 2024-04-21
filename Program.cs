using CleanHub.Config;
using CleanHub.Controllers;
using CleanHub.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using CleanHub.Extensions;
using Microsoft.AspNetCore.Localization;

namespace CleanHub
{
    public class Program
    {
        public  static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var config = new ConfigurationBuilder()
             .SetBasePath(Directory.GetCurrentDirectory())
             .AddJsonFile($"appsettings.json")
             .AddEnvironmentVariables()
             .Build();
            var culture = new CultureInfo("mk-MK");
            var supportedCultures = new List<CultureInfo> { };
            culture.DateTimeFormat.ShortDatePattern = "dd/MM/yyyy";
            culture.DateTimeFormat.DateSeparator = "/";
            supportedCultures.Add(culture);
            config.AddConfiguration<CompanyConfig>(builder.Services, "Company");

            config.AddConfiguration<SMTPConfig>(builder.Services, "SMTP");
            // Add services to the container.
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));
            builder.Services.AddDatabaseDeveloperPageExceptionFilter();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30); // Session Timeout nach 30 Minuten
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });
            builder.Services.AddControllersWithViews();
    
            builder.Services.AddIdentity<IdentityUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            builder.Services.AddDataProtection();
            builder.Services.AddRazorPages();
            builder.Services.AddTransient<HomeController>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseRequestLocalization(new RequestLocalizationOptions
            {
                DefaultRequestCulture = new RequestCulture(culture),
                SupportedCultures = supportedCultures,
                SupportedUICultures = supportedCultures
            });
            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();
            app.UseAuthentication();
            app.UseSession(); // Aktiviert die Session

            app.MapControllerRoute(
                   name: "default",
                   pattern: "{controller=Residents}/{action=Index}");
            app.MapControllerRoute(
               name: "area",
               pattern: "{area:Identity}/{controller=Account}/{action=Login}/{id?}");
            app.MapAreaControllerRoute(
                    "Identity",
                    "Identity",
                    "{controller = Home}/{action=Index}/{id?}");
            app.MapRazorPages();
            using (var serviceProvider = builder.Services.BuildServiceProvider())
            {
                CreateAdminWithRole.Create(serviceProvider).Wait();
            }
            app.Run();
        }
    }
}
