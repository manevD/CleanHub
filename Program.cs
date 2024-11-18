using AutoMapper;
using CleanHub.CleanHub.Infrastructure.Data;
using CleanHub.Config;
using CleanHub.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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
            config.AddConfiguration<CompanyConfig>(builder.Services, "Company");
            config.AddConfiguration<SMTPConfig>(builder.Services, "SMTP");
            // Add services to the container.
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));
            builder.Services.AddDatabaseDeveloperPageExceptionFilter();
            builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            builder.Services.AddMemoryCache();

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
            var configuration = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile(new App());
                cfg.AddProfile<App>();
                cfg.AddMaps(typeof(Profile));
            });

            IMapper mapper = configuration.CreateMapper();

            builder.Services.AddSingleton(mapper);
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
       
            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();
            app.UseAuthentication();
            app.UseSession(); // Aktiviert die Session

            app.MapControllerRoute(
                   name: "default",
                   pattern: "{controller=Customers}/{action=Index}");
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
                CreateStaticData.CreateUsers(serviceProvider).Wait();
               // CreateStaticData.SetDocumentStatus();
            }
            app.Run();
        }
    }
}
