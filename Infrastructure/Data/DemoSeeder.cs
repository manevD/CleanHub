using CleanHub.Entities;
using CleanHub.Entities.Enums;
using CleanHub.Helpers;
using CleanHub.Services;
using Microsoft.EntityFrameworkCore;

namespace CleanHub.Infrastructure.Data
{
    /// <summary>
    /// ДЕМО податоци (само гранката „demo“): 5 згради × 20 станари, фактури јануари–октомври 2026,
    /// уплати, долгови, претплати и резервен фонд.
    ///
    /// Се извршува само кога <c>Demo:Seed = true</c> во appsettings и базата нема ниту една зграда,
    /// па никогаш не допира база со вистински податоци.
    /// Сите податоци се измислени: е-пошта на @example.com и телефони што не постојат (+389 00 …).
    /// </summary>
    public static class DemoSeeder
    {
        private const int Year = 2026;
        private const int LastMonth = 10;          // фактури до октомври 2026
        private const int TenantsPerBuilding = 20;

        private static readonly string[] FirstNames =
        {
            "Александар", "Бојан", "Ване", "Горан", "Дејан", "Зоран", "Игор", "Јован", "Кире", "Лазар",
            "Марко", "Никола", "Петар", "Ристо", "Стојан", "Тони", "Филип", "Христијан", "Ана", "Билјана",
            "Валентина", "Гордана", "Даниела", "Елена", "Жаклина", "Ивана", "Катерина", "Лидија", "Марија",
            "Наташа", "Оливера", "Ружица", "Сашка", "Тања", "Виолета", "Снежана"
        };

        private static readonly string[] LastNames =
        {
            "Манчев", "Ковачевски", "Донев", "Ристов", "Петров", "Митров", "Николовски", "Стефков",
            "Панделиев", "Ѓорѓиев", "Трајков", "Јовановски", "Атанасов", "Стојанов", "Илиев", "Костов",
            "Димитров", "Велков", "Ангелов", "Тодоров", "Блажевски", "Мицев", "Зафиров", "Арсов"
        };

        private record BuildingDef(string Name, string BankAccount, int ReserveFund, float Cleaning, float Energy, bool Lift);

        private static readonly BuildingDef[] Buildings =
        {
            new("Маршал Тито 12/1",        "210-0000000012-01", 100, 250f, 70f,  false),
            new("Гоце Делчев 33/2",        "210-0000000033-02", 120, 220f, 85f,  true),
            new("Климент Охридски 7/3",    "210-0000000007-03", 100, 280f, 64f,  false),
            new("Братство Единство 18",    "210-0000000018-04", 150, 300f, 92f,  true),
            new("Браќа Миладинови 5/1",    "210-0000000005-05",  80, 200f, 58f,  false)
        };

        public static async Task SeedAsync(IServiceProvider services, IConfiguration config, ILogger logger)
        {
            if (!config.GetValue<bool>("Demo:Seed")) return;

            var db = services.GetRequiredService<ApplicationDbContext>();

            // Стара шема (претходна миграција) во демо базата → избриши сè и креирај одново
            await DropOldSchemaIfNeededAsync(db, logger);

            // Нова празна база → креирај ја шемата (миграција Initial)
            await db.Database.MigrateAsync();

            if (await db.Buildings.AnyAsync())
            {
                logger.LogInformation("Demo: базата веќе има податоци, не се внесува ништо.");
                return;
            }

            logger.LogInformation("Demo: се внесуваат тест податоци...");
            var rnd = new Random(2027); // секогаш исти податоци

            await SeedLookupsAsync(db);

            db.ChangeTracker.AutoDetectChangesEnabled = false;

            // Стандардни продукти (исто како SQLScripts/ProductsAdd.sql)
            db.Products.AddRange(
                new Product { ArticleNotes = "Чистење на влез за", UnitOfMeasurement = "мес.", Tax = 18, Quantity = 1 },
                new Product { ArticleNotes = "Управител за", UnitOfMeasurement = "бр.", Tax = 18, Quantity = 1 },
                new Product { ArticleNotes = "Резервен фонд за", UnitOfMeasurement = "бр.", Tax = 0, Quantity = 1 },
                new Product { ArticleNotes = "Потрошена електр. енер. за", UnitOfMeasurement = "бр.", Tax = 18, Quantity = 1 },
                new Product { ArticleNotes = "Комунална такса за јавно осветлување", UnitOfMeasurement = "бр.", Tax = 0, Quantity = 1 },
                new Product { ArticleNotes = "Административни трошоци", UnitOfMeasurement = "бр.", Tax = 18, Quantity = 1 },
                new Product { ArticleNotes = "Одржување на сметки за", UnitOfMeasurement = "бр.", Tax = 0, Quantity = 1 },
                new Product { ArticleNotes = "Одржување на лифт за", UnitOfMeasurement = "бр.", Tax = 18, Quantity = 1 });

            var docNumber = 14000;
            var paymentNumber = 5000;

            foreach (var def in Buildings)
            {
                // ---------- Зграда (само во Buildings — без посебен партнер во Customers) ----------
                var building = new Building
                {
                    Name = def.Name,
                    BankAccount = def.BankAccount,
                    ReserveFund = def.ReserveFund,
                    Customers = new List<Customer>()
                };
                db.Buildings.Add(building);

                // Продукти на зградата (за креирање нови фактури од апликацијата)
                building.BuildingProducts = BuildingProductsFor(def).ToList();

                // ---------- Станари ----------
                var tenants = new List<(Customer Customer, string Profile)>();
                for (var unit = 1; unit <= TenantsPerBuilding; unit++)
                {
                    var first = FirstNames[rnd.Next(FirstNames.Length)];
                    var last = LastNames[rnd.Next(LastNames.Length)];
                    var profile = ProfileFor(unit, rnd);

                    var tenant = new Customer
                    {
                        CustomerInfo = $"{first} {last} ст.{unit}",
                        ApartmentUnit = unit,
                        Adress = def.Name,
                        Email = unit % 7 == 0 ? null : $"{Latin(first)}.{Latin(last)}{unit}@example.com",
                        PhoneNumber = unit % 5 == 0 ? null : $"+389 00 {rnd.Next(100, 999)} {rnd.Next(100, 999)}",
                        Inactive = false,
                        ActivityId = 1,
                        PhysicalPerson = true,
                        Garage = unit % 6 == 0,
                        ActiveDatum = new DateOnly(2021, 1, 1),
                        Building = building,
                        Subscription = profile == "subscription" ? 3000 : null,
                        SubscriptionDate = profile == "subscription" ? new DateOnly(Year, 1, 1) : null,
                        SubscriptionDescription = profile == "subscription" ? "Претплата за 2026" : null,
                        BookFinancials = new List<BookFinancial>(),
                        Documents = new List<Document>()
                    };
                    building.Customers.Add(tenant);
                    db.Customers.Add(tenant);
                    tenants.Add((tenant, profile));
                }

                // ---------- Фактури јануари–октомври ----------
                for (var month = 1; month <= LastMonth; month++)
                {
                    var date = new DateOnly(Year, month, DateTime.DaysInMonth(Year, month));
                    var received = date.AddDays(10);
                    var monthText = DocumentService.GetMonthAsString(month) + " " + Year;
                    var mmYyyy = $"{month:00}/{Year}";
                    var energy = (float)Math.Round(def.Energy * (0.85 + rnd.NextDouble() * 0.3), 2);
                    var energyTotal = 0f;

                    foreach (var (tenant, profile) in tenants)
                    {
                        var books = BooksFor(def, mmYyyy, energy);
                        energyTotal += books.First(b => b.ArticleNotes!.StartsWith("Потрошена")).Total ?? 0;
                        var total = (float)Math.Round(books.Sum(b => b.Total ?? 0), MidpointRounding.AwayFromZero);
                        var reserve = books.First(b => b.ArticleNotes == "Резервен фонд").Total ?? 0;

                        var doc = new Document
                        {
                            Number = ++docNumber,
                            Date = date,
                            DateReceived = received,
                            ToDocument = monthText,
                            Description = def.Name,
                            TotalInput = 0,
                            TotalOutput = total,
                            CreatedTime = date.ToDateTime(new TimeOnly(9, 0)),
                            Customer = tenant,
                            Books = books,
                            PaymentStatus = PaymentStatus.Неплатено
                        };
                        db.Documents.Add(doc);

                        if (!IsPaid(profile, month, rnd)) continue;

                        // ---------- Уплата (како CreateBookFinancialAndReserve) ----------
                        var type = profile == "subscription" ? PaymentType.Subscription : PickPaymentType(rnd);
                        var payDate = received.AddDays(rnd.Next(-8, 6));
                        var payNo = type is PaymentType.Subscription or PaymentType.CashRegister ? null : (++paymentNumber).ToString();
                        var description = type switch
                        {
                            PaymentType.Subscription => "Платено од претплата за " + monthText,
                            PaymentType.Web => $"за {mmYyyy} по веб",
                            _ => $"за {mmYyyy}"
                        };

                        doc.PaymentStatus = PaymentStatus.Платено;
                        doc.PaymentType = type;
                        doc.PaymentDate = payDate;
                        doc.PaymentNumber = payNo;
                        doc.PaymentDescription = description;

                        db.BookFinancials.Add(new BookFinancial
                        {
                            InvoiceId = Constants.Recieve,
                            Document = doc,
                            Customer = tenant,
                            DocumentTypId = 4,
                            Demands = total,
                            Owes = 0,
                            DatumF = received,
                            Time = payDate.ToDateTime(new TimeOnly(10, 0)),
                            Status = PaymentStatus.Платено,
                            PaymentType = type,
                            PaymentDate = payDate,
                            PaymentNumber = payNo,
                            Description = description
                        });
                        db.BookFinancials.Add(new BookFinancial
                        {
                            InvoiceId = Constants.Reserve,
                            Document = doc,
                            Customer = tenant,
                            DocumentTypId = 4,
                            Demands = reserve,
                            Owes = 0,
                            DatumF = received,
                            Time = payDate.ToDateTime(new TimeOnly(10, 0)),
                            Status = PaymentStatus.Платено,
                            PaymentType = type,
                            PaymentDate = payDate,
                            PaymentNumber = payNo,
                            Description = description
                        });

                        if (type == PaymentType.Subscription && tenant.Subscription.HasValue)
                            tenant.Subscription = Math.Max(0, tenant.Subscription.Value - (int)Math.Round(total));
                    }

                    // Трошоци на зградата (резервен фонд 1201, книжени на самата зграда)
                    foreach (var (desc, amount) in BuildingCostsFor(def, month, rnd))
                    {
                        db.BookFinancials.Add(new BookFinancial
                        {
                            InvoiceId = Constants.Reserve,
                            Building = building,
                            DocumentTypId = 5,
                            Owes = amount,
                            Demands = 0,
                            DatumF = date,
                            Time = date.ToDateTime(new TimeOnly(12, 0)),
                            Status = PaymentStatus.Платено,
                            Description = desc
                        });
                    }

                    // Струја за зградата (SpecialInvoice 4001)
                    db.SpecialInvoices.Add(new SpecialInvoice
                    {
                        InvoiceId = Constants.Energy,
                        Building = building,
                        ForDate = date,
                        Total = Math.Round((decimal)energyTotal, MidpointRounding.AwayFromZero),
                        Status = month < LastMonth ? PaymentStatus.Платено : PaymentStatus.Неплатено
                    });
                }

                // ---------- Стари долгови (почетно салдо 2025) за должниците ----------
                foreach (var (tenant, _) in tenants.Where(t => t.Profile == "debtor"))
                {
                    db.BookFinancials.Add(new BookFinancial
                    {
                        InvoiceId = Constants.Recieve,
                        Customer = tenant,
                        DocumentTypId = 1,
                        Owes = rnd.Next(3, 9) * 500,
                        Demands = 0,
                        DatumF = new DateOnly(2025, 12, 31),
                        Time = new DateTime(2026, 1, 1, 8, 0, 0),
                        Status = PaymentStatus.Неплатено,
                        Description = "Почетно салдо 2025"
                    });
                }
            }

            db.ChangeTracker.DetectChanges();
            await db.SaveChangesAsync();

            logger.LogInformation("Demo: готово — {Buildings} згради, {Tenants} станари, {Docs} фактури.",
                Buildings.Length, Buildings.Length * TenantsPerBuilding, await db.Documents.CountAsync());
        }

        /// <summary>
        /// Ги брише сите тест податоци и ги внесува од почеток (шифрарниците и корисниците остануваат).
        /// Заштита: работи само ако името на базата содржи „demo“.
        /// </summary>
        public static async Task ResetAsync(IServiceProvider services, IConfiguration config, ILogger logger)
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            var dbName = db.Database.GetDbConnection().Database ?? "";
            if (!dbName.Contains("demo", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Demo reset: базата „{Db}“ не е демо база — ништо не е избришано.", dbName);
                return;
            }

            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                await db.Database.ExecuteSqlRawAsync(@"
                    DELETE FROM [Books];
                    DELETE FROM [BookFinancials];
                    DELETE FROM [SpecialInvoices];
                    DELETE FROM [Documents];
                    DELETE FROM [BuildingProducts];
                    DELETE FROM [Customers];
                    DELETE FROM [Buildings];
                    DELETE FROM [Products];");
                await tx.CommitAsync();
            }

            db.ChangeTracker.Clear();
            logger.LogInformation("Demo reset: податоците се избришани, се внесуваат повторно.");
            await SeedAsync(services, config, logger);
        }

        /// <summary>
        /// Ако демо базата е креирана со постара миграција (друга шема), сите табели се бришат
        /// за новата миграција да ја креира шемата од нула. Само за база чие име содржи „demo“.
        /// </summary>
        private static async Task DropOldSchemaIfNeededAsync(ApplicationDbContext db, ILogger logger)
        {
            var known = db.Database.GetMigrations().ToHashSet();
            var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
            if (applied.Count == 0 || applied.All(known.Contains)) return;

            var dbName = db.Database.GetDbConnection().Database ?? "";
            if (!dbName.Contains("demo", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Demo: базата „{Db}“ има стари миграции, но не е демо база — ништо не е избришано.", dbName);
                return;
            }

            logger.LogWarning("Demo: стара шема ({Old}) — сите табели се бришат и се креираат одново.", string.Join(", ", applied));
            await db.Database.ExecuteSqlRawAsync(@"
                DECLARE @sql nvarchar(max) = N'';
                SELECT @sql += N'ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N' DROP CONSTRAINT ' + QUOTENAME(f.name) + N';'
                FROM sys.foreign_keys f
                JOIN sys.tables t ON f.parent_object_id = t.object_id
                JOIN sys.schemas s ON t.schema_id = s.schema_id;
                EXEC sp_executesql @sql;

                SET @sql = N'';
                SELECT @sql += N'DROP TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N';'
                FROM sys.tables t
                JOIN sys.schemas s ON t.schema_id = s.schema_id;
                EXEC sp_executesql @sql;");
        }

        // Месечни трошоци на зградата (сметки, материјали, поправки)
        private static IEnumerable<(string Description, double Amount)> BuildingCostsFor(BuildingDef def, int month, Random rnd)
        {
            yield return ($"Средства за чистење {month:00}/{Year}", Math.Round(800 + rnd.NextDouble() * 700, MidpointRounding.AwayFromZero));
            if (def.Lift)
                yield return ($"Сервис на лифт {month:00}/{Year}", 2400);
            if (month % 3 == 0)
                yield return ("Замена на сијалици во влез", Math.Round(600 + rnd.NextDouble() * 900, MidpointRounding.AwayFromZero));
            if (month == 7)
                yield return ("Поправка на влезна врата", Math.Round(4500 + rnd.NextDouble() * 3000, MidpointRounding.AwayFromZero));
        }

        // ------------------------------------------------------------------
        // Шифрарници со фиксни ID-а (1200/1201/4001, дејност 2 = зграда …)
        // ------------------------------------------------------------------
        private static async Task SeedLookupsAsync(ApplicationDbContext db)
        {
            await using var tx = await db.Database.BeginTransactionAsync();

            async Task Insert(string table, string sql)
            {
                await db.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT [{table}] ON; {sql}; SET IDENTITY_INSERT [{table}] OFF;");
            }

            if (!await db.Activity.AnyAsync())
                await Insert("Activity",
                    "INSERT INTO [Activity] (Id, Name) VALUES (1, N'Станар'), (2, N'Зграда'), (3, N'Фирма'), (4, N'Добавувач')");

            if (!await db.Set<Invoice>().AnyAsync())
                await Insert("Invoice",
                    "INSERT INTO [Invoice] (Id, Description, KarticaPar, FromBilance) VALUES " +
                    "(1200, N'Редовен фонд', 1, 1), (1201, N'Резервен фонд', 1, 1), (4001, N'Струја', 0, 0)");

            if (!await db.Set<DocumentTyp>().AnyAsync())
                await Insert("DocumentTyp",
                    "INSERT INTO [DocumentTyp] (Id, VidDokument) VALUES " +
                    "(1, N'Почетно салдо'), (2, N'Фактура'), (3, N'Извод'), (4, N'Уплата'), (5, N'Каса')");

            if (!await db.Banks.AnyAsync())
                await db.Database.ExecuteSqlRawAsync(
                    "INSERT INTO [Banks] (Name) VALUES (N'Комерцијална банка'), (N'НЛБ Банка'), (N'Стопанска банка'), (N'ТТК Банка'), (N'Халк Банка')");

            await tx.CommitAsync();
        }

        private static IEnumerable<BuildingProduct> BuildingProductsFor(BuildingDef def)
        {
            yield return new BuildingProduct { ArticleNotes = "Административни трошоци", UnitOfMeasurement = "бр.", Quantity = 1, Price = 30f, Tax = 18 };
            yield return new BuildingProduct { ArticleNotes = "Потрошена електр. енер. за", UnitOfMeasurement = "бр.", Quantity = 1, Price = def.Energy, Tax = 18 };
            yield return new BuildingProduct { ArticleNotes = "Комунална такса за јавно осветлување", UnitOfMeasurement = "бр.", Quantity = 1, Price = 11.5f, Tax = 0 };
            yield return new BuildingProduct { ArticleNotes = "Одржување на сметки за", UnitOfMeasurement = "бр.", Quantity = 1, Price = 15f, Tax = 0 };
            yield return new BuildingProduct { ArticleNotes = "Резервен фонд за", UnitOfMeasurement = "бр.", Quantity = 1, Price = def.ReserveFund, Tax = 0 };
            yield return new BuildingProduct { ArticleNotes = "Управител за", UnitOfMeasurement = "бр.", Quantity = 1, Price = 100f, Tax = 18 };
            yield return new BuildingProduct { ArticleNotes = "Чистење на влез за", UnitOfMeasurement = "мес.", Quantity = 1, Price = def.Cleaning, Tax = 18 };
            if (def.Lift)
                yield return new BuildingProduct { ArticleNotes = "Одржување на лифт за", UnitOfMeasurement = "бр.", Quantity = 1, Price = 120f, Tax = 18 };
        }

        // Ставки на фактурата — ист изглед како вистинските фактури
        private static List<Book> BooksFor(BuildingDef def, string mmYyyy, float energy)
        {
            var list = new List<Book>
            {
                Line("Административни трошоци", "бр.", 30f, 18),
                Line($"Потрошена електрична енергија за месец {mmYyyy}", "бр.", energy, 18),
                Line("Комунална такса за јавно осветлување", "бр.", 11.5f, 0),
                Line("Одржување на сметки", "бр.", 15f, 0),
                Line("Резервен фонд", "бр.", def.ReserveFund, 0),
                Line($"Управител за месец {mmYyyy}", "бр.", 100f, 18),
                Line($"Чистење за месец {mmYyyy}", "мес", def.Cleaning, 18)
            };
            if (def.Lift)
                list.Add(Line($"Одржување на лифт за месец {mmYyyy}", "бр.", 120f, 18));
            return list;
        }

        private static Book Line(string notes, string unit, float price, float tax)
        {
            // Ставката: 2 децимали
            var withTax = (float)Math.Round(price * (1 + tax / 100f), 2);
            return new Book
            {
                ArticleNotes = notes,
                UnitOfMeasurement = unit,
                Quantity = 1,
                Price = (float)Math.Round(price, 2),
                Tax = tax,
                PriceWithTax = withTax,
                Total = withTax,
                Input = 0,
                Output = withTax
            };
        }

        // Профил на плаќање по станар
        private static string ProfileFor(int unit, Random rnd)
        {
            if (unit == 4) return "subscription";               // по еден со претплата во секоја зграда
            if (unit is 9 or 17) return "debtor";                // по двајца должници (3+ месеци)
            var r = rnd.NextDouble();
            if (r < 0.18) return "late";                         // доцнат 1–2 месеци
            return "good";
        }

        private static bool IsPaid(string profile, int month, Random rnd) => profile switch
        {
            "subscription" => true,
            "debtor" => month <= LastMonth - 4 && rnd.NextDouble() < 0.7,   // последните 4 месеци неплатени
            "late" => month <= LastMonth - 2,                               // последните 2 месеци неплатени
            _ => month < LastMonth || rnd.NextDouble() < 0.55               // октомври: дел уште не платиле
        };

        private static PaymentType PickPaymentType(Random rnd)
        {
            var r = rnd.NextDouble();
            return r < 0.45 ? PaymentType.Bank
                 : r < 0.70 ? PaymentType.Post
                 : r < 0.90 ? PaymentType.Web
                 : PaymentType.CashRegister;
        }

        // За е-пошта (example.com) — кирилица во латиница
        private static string Latin(string text)
        {
            var map = new Dictionary<char, string>
            {
                ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['ѓ'] = "gj", ['е'] = "e", ['ж'] = "zh",
                ['з'] = "z", ['ѕ'] = "dz", ['и'] = "i", ['ј'] = "j", ['к'] = "k", ['л'] = "l", ['љ'] = "lj", ['м'] = "m",
                ['н'] = "n", ['њ'] = "nj", ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t", ['ќ'] = "kj",
                ['у'] = "u", ['ф'] = "f", ['х'] = "h", ['ц'] = "c", ['ч'] = "ch", ['џ'] = "dj", ['ш'] = "sh"
            };
            return string.Concat(text.ToLowerInvariant().Select(c => map.TryGetValue(c, out var s) ? s : c.ToString()));
        }
    }
}
