using AutoMapper;
using CleanHub.Config;
using CleanHub.Extensions;
using CleanHub.Infrastructure.Data;
using CleanHub.Infrastructure.Repositories;
using CleanHub.Infrastructure.Repositories.Interfaces;
using CleanHub.Services.Ai;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using System.Configuration;
using System.Globalization;
using System.IO.Compression;

namespace CleanHub
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // ----------------------------------------------------
            // Configuration
            // ----------------------------------------------------
            // builder.Configuration = appsettings.json + appsettings.{Environment}.json
            // + user-secrets (Development) + променливи на околина
            builder.Configuration.AddConfiguration<CompanyConfig>(builder.Services, "Company");
            builder.Configuration.AddConfiguration<SMTPConfig>(builder.Services, "SMTP");

            var connectionString =
                builder.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            var connectionStringMarti =
                builder.Configuration.GetConnectionString("DefaultConnectionMarti")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnectionMarti' not found.");

            // ----------------------------------------------------
            // DbContexts
            // ----------------------------------------------------
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
              options.UseSqlServer(
                    connectionString,
                    sql =>
                    {
                        sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                        sql.CommandTimeout(120);
                    }
                ));


            builder.Services.AddDbContext<ApplicationDbMartiContext>(options =>
                options.UseMySql(
                    connectionStringMarti,
                    new MySqlServerVersion(new Version(8, 0, 32)),
                    mysql =>
                    {
                        mysql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                        mysql.EnableRetryOnFailure(
                            maxRetryCount: 3,
                            maxRetryDelay: TimeSpan.FromSeconds(5),
                            errorNumbersToAdd: null
                        );
                    }
                ));

            builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            // ----------------------------------------------------
            // Services
            // ----------------------------------------------------
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

            // ----------------------------------------------------
            // AI (клучот: user-secrets / env AI__ApiKey — никогаш во appsettings.json)
            // ----------------------------------------------------
            builder.Services.AddOptions<AiConfig>().Bind(builder.Configuration.GetSection("AI"));
            builder.Services.AddHttpClient<ClaudeClient>();
            builder.Services.AddScoped<FinanceDataService>();
            builder.Services.AddScoped<EmailSender>();
            builder.Services.AddScoped<DebtReminderService>();
            builder.Services.AddScoped<AnomalyService>();
            builder.Services.AddScoped<AssistantService>();
            builder.Services.AddScoped<AnnualReportService>();
            builder.Services.AddHostedService<WeeklyReportHostedService>();

            // ----------------------------------------------------
            // Session
            // ----------------------------------------------------
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            // ----------------------------------------------------
            // Identity
            // ----------------------------------------------------
            builder.Services.AddIdentity<IdentityUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = "/Identity/Account/Login";
                options.AccessDeniedPath = "/Identity/Account/AccessDenied";
                options.ReturnUrlParameter = "returnUrl";
                options.Events.OnRedirectToReturnUrl = context =>
                {
                    context.Response.Redirect(
                        string.IsNullOrEmpty(context.Request.Query["returnUrl"])
                            ? "/Buildings/Index"
                            : context.RedirectUri
                    );
                    return Task.CompletedTask;
                };
            });

            // ----------------------------------------------------
            // MVC / Razor / AutoMapper
            // ----------------------------------------------------
            builder.Services.AddControllersWithViews();
            builder.Services.AddRazorPages();
            builder.Services.AddDataProtection();

            var mapperConfig = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile(new App());
                cfg.AddProfile<App>();
                cfg.AddMaps(typeof(Profile));
            });
            builder.Services.AddSingleton(mapperConfig.CreateMapper());

            // ----------------------------------------------------
            // Compression
            // ----------------------------------------------------
            builder.Services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.Providers.Add<BrotliCompressionProvider>();
                options.Providers.Add<GzipCompressionProvider>();
                options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "image/svg+xml" });
            });
            builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
            builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

            // ----------------------------------------------------
            // Build app
            // ----------------------------------------------------
            var app = builder.Build();

            // ----------------------------------------------------
            // Culture
            // ----------------------------------------------------
            var culture = new CultureInfo("mk-MK");
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            app.UseRequestLocalization(new RequestLocalizationOptions
            {
                DefaultRequestCulture = new RequestCulture("mk-MK"),
                SupportedCultures = new[] { culture },
                SupportedUICultures = new[] { culture }
            });

            // ----------------------------------------------------
            // Pipeline
            // ----------------------------------------------------
            if (app.Environment.IsDevelopment())
            {
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            // Компресија (Brotli/Gzip) за HTML, CSS, JS и JSON — мора пред статичните фајлови
            app.UseResponseCompression();

            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = ctx =>
                {
                    ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=31536000");
                }
            });

            app.UseRouting();

            // 🔥 WICHTIGE REIHENFOLGE
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseSession();

            // ----------------------------------------------------
            // Routes
            // ----------------------------------------------------
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Buildings}/{action=Index}");

            app.MapControllerRoute(
                name: "area",
                pattern: "{area:Identity}/{controller=Account}/{action=Login}/{id?}");

            app.MapRazorPages();
            app.Use(async (context, next) =>
            {
                context.Response.Headers["Content-Security-Policy"] =
                    "script-src 'self' 'unsafe-eval' 'unsafe-inline';";
                await next();
            });
            // ----------------------------------------------------
            // Startup-Tasks (KORREKT)
            // ----------------------------------------------------
            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                CreateStaticData.CreateUsers(services).Wait();
            }

            app.Run();
        }
    }
}
