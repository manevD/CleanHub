using System.Globalization;
using AutoMapper;
using CleanHub.Config;
using CleanHub.Extensions;
using CleanHub.Infrastructure.Data;
using CleanHub.Infrastructure.Repositories;
using CleanHub.Infrastructure.Repositories.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
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
                options.UseSqlServer(connectionString, options =>
                    options.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));
            builder.Services.AddDatabaseDeveloperPageExceptionFilter();
            builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            builder.Services.AddMemoryCache();
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
            builder.Services.AddScoped<ICustomerRepository, CustomersRepository>();
            builder.Services.AddScoped<IDocumentsRepository, DocumentRepository>();
            builder.Services.AddScoped<IBuildingProductRepository, BuildingProductRepository>();
            builder.Services.AddScoped<IBuildingRepository, BuildingRepository>();
            builder.Services.AddScoped<IProductRepository, ProductRepository>();
            builder.Services.AddScoped<IBookFinancialsRepository, BookFinancialRepository>();
            builder.Services.AddScoped<IBookRepository, BookRepository>();
            builder.Services.AddScoped<ISpecialInvoiceRepository, SpecialInvoiceRepository>();
            var cultures = new[]
            {
                new CultureInfo("en-US"),
                new CultureInfo("de"),
            };

            
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
            builder.Services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
            });
            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = "/Identity/Account/Login"; // Standard-Login-Pfad
                options.AccessDeniedPath = "/Identity/Account/AccessDenied"; // Zugriff verweigert
                options.ReturnUrlParameter = "returnUrl";
                options.Events.OnRedirectToReturnUrl = context =>
                {
                    // Wenn kein ReturnUrl definiert ist, navigiere zu Buildings/Index
                    context.Response.Redirect(string.IsNullOrEmpty(context.Request.Query["returnUrl"])
                        ? "/Buildings/Index"
                        : context.RedirectUri);
                    return Task.CompletedTask;
                };
            });
            IMapper mapper = configuration.CreateMapper();

            builder.Services.AddSingleton(mapper);
            var app = builder.Build();
            app.UseRequestLocalization(new RequestLocalizationOptions
            {
                DefaultRequestCulture = new RequestCulture("de-DE"),
                SupportedCultures = cultures,
                SupportedUICultures = cultures
            });
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
            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = ctx =>
                {
                    ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=31536000");
                }
            });
            app.UseRouting();

            app.UseAuthorization();
            app.UseAuthentication();
            app.UseSession();
            app.UseResponseCompression();

            app.MapControllerRoute(
                   name: "default",
                   pattern: "{controller=Buildings}/{action=Index}");
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
