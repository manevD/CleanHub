using System.Globalization;
using CleanHub.Entities;
using CleanHub.Entities.Enums;
using CleanHub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CleanHub.Services.Ai
{
    public enum ReminderLevel
    {
        Polite = 1,   // Љубезно
        Reminder = 2, // Потсетник
        Warning = 3   // Опомена
    }

    public class DebtorInfo
    {
        public int CustomerId { get; set; }
        public string CustomerInfo { get; set; } = "";
        public int? BuildingId { get; set; }
        public string? BuildingName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public int UnpaidCount { get; set; }
        public double UnpaidTotal { get; set; }
        public DateOnly? OldestDate { get; set; }
        public List<string> Periods { get; set; } = new();
        public ReminderLevel Level { get; set; }
    }

    public class CostLine
    {
        public DateOnly? Date { get; set; }
        public string Description { get; set; } = "";
        public double Amount { get; set; }
    }

    public class BuildingYearStats
    {
        public int BuildingId { get; set; }
        public string BuildingName { get; set; } = "";
        public int Year { get; set; }
        public int TenantsCount { get; set; }
        public int InvoicesCount { get; set; }
        public double Invoiced { get; set; }
        public double CollectedRegular { get; set; }
        public double CollectedReserve { get; set; }
        public double ReserveCosts { get; set; }
        public double UnpaidTotal { get; set; }
        public int TenantsWithDebt { get; set; }
        public double ReserveBalanceEndOfYear { get; set; }
        public double ReserveBalanceNow { get; set; }
        public List<CostLine> Costs { get; set; } = new();
        public double CollectionRate => Invoiced > 0 ? Math.Round(CollectedRegular / Invoiced * 100, 1) : 0;
    }

    public class CustomerBalance
    {
        public int CustomerId { get; set; }
        public string CustomerInfo { get; set; } = "";
        public string? BuildingName { get; set; }
        public double Owes { get; set; }
        public double Demands { get; set; }
        public double Saldo => Owes - Demands;
        public List<object> UnpaidInvoices { get; set; } = new();
        public List<object> LastPayments { get; set; } = new();
    }

    /// <summary>
    /// Само читање (AsNoTracking) — податоци за AI функциите. Ништо не се запишува во базата.
    /// Сумите се пресметуваат на ист начин како на постоечките картици.
    /// </summary>
    public class FinanceDataService
    {
        public static readonly CultureInfo Mk = new("mk-MK");
        public static string Money(double value) => value.ToString("N2", Mk) + " МКД";

        private static readonly PaymentStatus[] UnpaidStatuses =
            { PaymentStatus.Неплатено, PaymentStatus.Делумно, PaymentStatus.Задоцнето };

        private readonly ApplicationDbContext _db;

        public FinanceDataService(ApplicationDbContext db)
        {
            _db = db;
        }

        public static ReminderLevel LevelFor(int unpaidCount) => unpaidCount switch
        {
            <= 1 => ReminderLevel.Polite,
            2 => ReminderLevel.Reminder,
            _ => ReminderLevel.Warning
        };

        public static string LevelName(ReminderLevel level) => level switch
        {
            ReminderLevel.Polite => "Љубезно",
            ReminderLevel.Reminder => "Потсетник",
            _ => "Опомена"
        };

        public async Task<List<(int Id, string Name)>> GetBuildingsAsync(CancellationToken ct = default)
        {
            var list = await _db.Buildings.AsNoTracking()
                .OrderBy(b => b.Name)
                .Select(b => new { b.Id, b.Name })
                .ToListAsync(ct);
            return list.Select(b => (b.Id, b.Name ?? $"Објект {b.Id}")).ToList();
        }

        /// <summary>
        /// Станари со заостанат долг. Износот е пресметан ИСТО како „Заостанат долг“ на фактурата;
        /// бројот на неплатени месеци (статус Неплатено / Делумно / Задоцнето) го одредува нивото.
        /// </summary>
        public async Task<List<DebtorInfo>> GetDebtorsAsync(int? buildingId = null, int minUnpaid = 1, CancellationToken ct = default)
        {
            var customers = await _db.Customers.AsNoTracking()
                .Where(c => !c.Hide && c.Inactive != true && (buildingId == null || c.BuildingId == buildingId))
                .Select(c => new
                {
                    c.Id,
                    c.CustomerInfo,
                    c.Email,
                    c.PhoneNumber,
                    c.BuildingId,
                    BuildingName = c.Building != null ? c.Building.Name : null
                })
                .ToListAsync(ct);

            var customerIds = customers.Select(c => c.Id).ToList();
            if (customerIds.Count == 0) return new List<DebtorInfo>();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var from2021 = new DateOnly(2021, 1, 1);

            // ---- „Заостанат долг“ — ИСТА логика како на фактурата (DocumentsController, GET CUSTOMER DEBTS) ----
            // долг (dolzi)   = фактури со DateReceived < денес и Date > 01.01.2021 (TotalOutput)
            //                + 1200 книжења со Owes != 0 и DatumF >= 01.01.2021 (Owes)
            // побарува       = сите 1200 книжења (Demands)
            // заостанат долг = dolzi - pobaruva, само ако dolzi > pobaruva
            var docsRows = await _db.Documents.AsNoTracking()
                .Where(d => d.CustomerId != null
                            && customerIds.Contains(d.CustomerId.Value)
                            && d.DateReceived.HasValue
                            && d.DateReceived.Value < today)
                .Select(d => new { CustomerId = d.CustomerId!.Value, d.TotalOutput, d.Date, d.ToDocument, d.PaymentStatus })
                .ToListAsync(ct);

            var bfRows = await _db.BookFinancials.AsNoTracking()
                .Where(b => b.CustomerId != null
                            && customerIds.Contains(b.CustomerId.Value)
                            && b.InvoiceId == (int)InvoiceTyp.Recieve)
                .Select(b => new { CustomerId = b.CustomerId!.Value, b.Demands, b.Owes, b.DatumF })
                .ToListAsync(ct);

            var docsByCustomer = docsRows.GroupBy(d => d.CustomerId).ToDictionary(g => g.Key, g => g.ToList());
            var bfByCustomer = bfRows.GroupBy(b => b.CustomerId).ToDictionary(g => g.Key, g => g.ToList());

            var result = new List<DebtorInfo>();
            foreach (var c in customers)
            {
                docsByCustomer.TryGetValue(c.Id, out var customerDocs);
                bfByCustomer.TryGetValue(c.Id, out var dataBookFinancial);
                customerDocs ??= new();
                dataBookFinancial ??= new();

                var pobaruva = dataBookFinancial.Sum(x => x.Demands);

                double dolzi = customerDocs
                    .Where(x => x.Date.HasValue && x.Date.Value > from2021)
                    .Sum(x => (double)(x.TotalOutput ?? 0));

                if (dataBookFinancial.Any(x => x.Owes != 0 && x.DatumF.HasValue && x.DatumF.Value >= from2021))
                {
                    dolzi += dataBookFinancial
                        .Where(x => x.Owes != 0 && x.DatumF.HasValue && x.DatumF.Value >= from2021)
                        .Sum(x => x.Owes);
                }

                if (!(dolzi > pobaruva)) continue; // нема заостанат долг — исто како на фактурата

                // Неплатени месеци (за нивото на потсетникот и листата на периоди)
                var unpaidDocs = customerDocs
                    .Where(d => UnpaidStatuses.Contains(d.PaymentStatus) && d.TotalOutput > 0)
                    .OrderBy(d => d.Date)
                    .ToList();
                var unpaidCount = Math.Max(1, unpaidDocs.Count);
                if (unpaidCount < minUnpaid) continue;

                result.Add(new DebtorInfo
                {
                    CustomerId = c.Id,
                    CustomerInfo = c.CustomerInfo,
                    Email = c.Email,
                    PhoneNumber = c.PhoneNumber,
                    BuildingId = c.BuildingId,
                    BuildingName = c.BuildingName,
                    UnpaidCount = unpaidCount,
                    UnpaidTotal = Math.Abs(pobaruva - dolzi),
                    OldestDate = unpaidDocs.FirstOrDefault()?.Date,
                    Periods = unpaidDocs
                        .Select(d => !string.IsNullOrWhiteSpace(d.ToDocument) ? d.ToDocument!.Trim() : d.Date?.ToString("MM.yyyy") ?? "")
                        .Where(p => p.Length > 0)
                        .ToList(),
                    Level = LevelFor(unpaidCount)
                });
            }

            return result
                .OrderByDescending(d => d.UnpaidCount)
                .ThenByDescending(d => d.UnpaidTotal)
                .ToList();
        }

        public async Task<List<object>> SearchCustomersAsync(string query, CancellationToken ct = default)
        {
            query = (query ?? "").Trim();
            if (query.Length == 0) return new List<object>();

            var list = await _db.Customers.AsNoTracking()
                .Where(c => !c.Hide && c.CustomerInfo.Contains(query))
                .OrderBy(c => c.CustomerInfo)
                .Take(15)
                .Select(c => new { id = c.Id, name = c.CustomerInfo, building = c.Building != null ? c.Building.Name : null, inactive = c.Inactive == true })
                .ToListAsync(ct);
            return list.Cast<object>().ToList();
        }

        /// <summary>Состојба на станар — иста логика како „Картица на станар 1200“.</summary>
        public async Task<CustomerBalance?> GetCustomerBalanceAsync(int customerId, CancellationToken ct = default)
        {
            var customer = await _db.Customers.AsNoTracking()
                .Where(c => c.Id == customerId)
                .Select(c => new { c.Id, c.CustomerInfo, BuildingName = c.Building != null ? c.Building.Name : null })
                .FirstOrDefaultAsync(ct);
            if (customer == null) return null;

            var from2021 = new DateOnly(2021, 1, 1);

            var docs = await _db.Documents.AsNoTracking()
                .Where(d => d.CustomerId == customerId)
                .Select(d => new { d.Id, d.Date, d.ToDocument, d.TotalOutput, d.PaymentStatus })
                .ToListAsync(ct);

            var financials = await _db.BookFinancials.AsNoTracking()
                .Where(x => x.CustomerId == customerId
                            && (x.InvoiceId == (int)InvoiceTyp.Recieve || x.DocumentTypId == 11)
                            && x.InvoiceId != (int)InvoiceTyp.Reserve)
                .Select(x => new { x.DatumF, x.Description, x.Owes, x.Demands })
                .ToListAsync(ct);

            var owes = docs.Where(d => d.Date > from2021).Sum(d => (double)(d.TotalOutput ?? 0))
                       + financials.Where(f => f.Owes != 0 && f.DatumF >= from2021).Sum(f => f.Owes);

            return new CustomerBalance
            {
                CustomerId = customer.Id,
                CustomerInfo = customer.CustomerInfo,
                BuildingName = customer.BuildingName,
                Owes = Math.Round(owes, 2),
                Demands = Math.Round(financials.Sum(f => f.Demands), 2),
                UnpaidInvoices = docs
                    .Where(d => UnpaidStatuses.Contains(d.PaymentStatus))
                    .OrderBy(d => d.Date)
                    .Select(d => (object)new { period = d.ToDocument, date = d.Date?.ToString("dd.MM.yyyy"), amount = Math.Round((double)(d.TotalOutput ?? 0), 2), status = d.PaymentStatus.ToString() })
                    .ToList(),
                LastPayments = financials
                    .Where(f => f.Demands > 0)
                    .OrderByDescending(f => f.DatumF)
                    .Take(6)
                    .Select(f => (object)new { date = f.DatumF?.ToString("dd.MM.yyyy"), description = f.Description, amount = Math.Round(f.Demands, 2) })
                    .ToList()
            };
        }

        /// <summary>Годишни бројки за објект (за годишен извештај и асистентот).</summary>
        public async Task<BuildingYearStats?> GetBuildingYearStatsAsync(int buildingId, int year, CancellationToken ct = default)
        {
            var building = await _db.Buildings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == buildingId, ct);
            if (building == null) return null;

            var start = new DateOnly(year, 1, 1);
            var end = new DateOnly(year, 12, 31);
            var refId = building.CustomerRefId ?? building.Id;

            var allCustomerIds = await _db.Customers.AsNoTracking()
                .Where(c => c.BuildingId == buildingId)
                .Select(c => new { c.Id, c.Hide, c.Inactive })
                .ToListAsync(ct);

            var tenantIds = allCustomerIds.Where(c => !c.Hide).Select(c => c.Id).ToList();
            var reserveIds = allCustomerIds.Select(c => c.Id).Append(refId).Distinct().ToList();

            // Фактури за годината
            var docs = await _db.Documents.AsNoTracking()
                .Where(d => d.CustomerId != null && tenantIds.Contains(d.CustomerId.Value) && d.Date >= start && d.Date <= end)
                .Select(d => new { CustomerId = d.CustomerId!.Value, d.TotalOutput, d.PaymentStatus })
                .ToListAsync(ct);

            // Уплати 1200 (редовен фонд) за годината
            var regular = await _db.BookFinancials.AsNoTracking()
                .Where(x => x.CustomerId != null && tenantIds.Contains(x.CustomerId.Value)
                            && x.InvoiceId == (int)InvoiceTyp.Recieve
                            && x.DatumF >= start && x.DatumF <= end)
                .Select(x => x.Demands)
                .ToListAsync(ct);

            // Резервен фонд 1201 — исто како „Завршна“ (објект + станари, без DontSum)
            var reserve = await _db.BookFinancials.AsNoTracking()
                .Where(x => x.CustomerId != null && reserveIds.Contains(x.CustomerId.Value)
                            && x.InvoiceId == (int)InvoiceTyp.Reserve
                            && !x.DontSum)
                .Select(x => new { x.CustomerId, x.DatumF, x.Description, x.Owes, x.Demands, x.DocumentTypId })
                .ToListAsync(ct);

            var reserveYear = reserve.Where(x => x.DatumF >= start && x.DatumF <= end).ToList();

            var costs = reserveYear
                .Where(x => x.CustomerId == refId && x.DocumentTypId == 5 && x.Owes > 0)
                .OrderBy(x => x.DatumF)
                .Select(x => new CostLine { Date = x.DatumF, Description = x.Description ?? "", Amount = Math.Round(x.Owes, 2) })
                .ToList();

            var unpaidDocs = docs.Where(d => UnpaidStatuses.Contains(d.PaymentStatus)).ToList();

            return new BuildingYearStats
            {
                BuildingId = building.Id,
                BuildingName = building.Name ?? $"Објект {building.Id}",
                Year = year,
                TenantsCount = allCustomerIds.Count(c => !c.Hide && c.Inactive != true),
                InvoicesCount = docs.Count,
                Invoiced = Math.Round(docs.Sum(d => (double)(d.TotalOutput ?? 0)), 2),
                CollectedRegular = Math.Round(regular.Sum(), 2),
                CollectedReserve = Math.Round(reserveYear.Sum(x => x.Demands), 2),
                ReserveCosts = Math.Round(costs.Sum(c => c.Amount), 2),
                UnpaidTotal = Math.Round(unpaidDocs.Sum(d => (double)(d.TotalOutput ?? 0)), 2),
                TenantsWithDebt = unpaidDocs.Select(d => d.CustomerId).Distinct().Count(),
                ReserveBalanceEndOfYear = Math.Round(reserve.Where(x => x.DatumF <= end).Sum(x => x.Demands - x.Owes), 2),
                ReserveBalanceNow = Math.Round(reserve.Sum(x => x.Demands - x.Owes), 2),
                Costs = costs
            };
        }
    }
}
