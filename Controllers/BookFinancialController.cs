using CleanHub.CleanHub.Infrastructure.Data;
using CleanHub.Entities;
using CleanHub.Extensions;
using CleanHub.Migrations;
using CleanHub.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PaymentStatus = CleanHub.Entities.PaymentStatus;

namespace CleanHub.Controllers
{
    public class BookFinancialController(ApplicationDbContext context) : Controller
    {
        private static DateOnly DateFrom = DateOnly.FromDateTime(DateTime.Now);
        private static DateOnly DateTo = DateOnly.FromDateTime(DateTime.Now);
        public List<Building> Buildings { get; set; } = context.Buildings.ToList();
        public IActionResult Index()
        {
            ViewBag.PaymentStatusList = Enum.GetValues(typeof(PaymentStatus))
                .Cast<PaymentStatus>()
                .Select(e => new SelectListItem
                {
                    Text = e.GetEnumDescription(),
                    Value = ((int)e).ToString(),
                })
                .ToList();
            ViewBag.InvoiceTypList = Enum.GetValues(typeof(InvoiceTyp))
                .Cast<InvoiceTyp>()
                .Select(e => new SelectListItem
                {
                    Text = e.GetEnumDescription(),
                    Value = ((int)e).ToString(),
                }).Where(x=>x.Text != "Струја")
                .ToList();
            if (!Buildings.Any())
            {
                throw new Exception("No buildings found in the database.");
            }
            Buildings.Insert(0, new Building
            {
                Name = "Сите",
                Id = 0
            });
            ViewBag.Buildings = new SelectList(Buildings, "Id", "Name");

            return View("Index");
        }

        public async Task<IActionResult> Books(int? invoiceId, int? buildingId, int? paymentStatusId,string dateFrom, string dateTo)
        {
            var result = await(from b in context.Buildings
                join c in context.Customers on b.Id equals c.BuildingId into customerJoin
                from c in customerJoin.DefaultIfEmpty()
                join bf in context.BookFinancials on c.Id equals bf.CustomerId into financialJoin
                from bf in financialJoin.DefaultIfEmpty()
                where bf.InvoiceId == invoiceId.Value && b.Id == buildingId.Value
                select new BookFinancialInfoViewModel
                {
                    Id = bf.Id,
                    BuildingName = b.Name,
                    CustomerInfo = c.CustomerInfo,
                    Status = bf.Status,
                    InvoiceId = bf.InvoiceId.Value,
                    Description = bf.Description,
                    DatumF = bf.DatumF.Value,
                    Owes = bf.Owes,
                    Demands = bf.Demands
                }).ToListAsync();
            
            Buildings.Insert(0, new Building()
            {
                Name = "Сите",
                Id = 0
            });
            if (!Buildings.Any())
            {
                throw new Exception("No buildings found in the database.");
            }
            ViewBag.PaymentStatusList = Enum.GetValues(typeof(PaymentStatus))
                .Cast<PaymentStatus>()
                .Select(e => new SelectListItem
                {
                    Text = e.GetEnumDescription(),
                    Value = ((int)e).ToString(),
                    Selected = (int)e == paymentStatusId // Markiere den ausgewählten Status
                })
                .ToList();
            ViewBag.Buildings = new SelectList(Buildings, "Id", "Name", buildingId);
            if (buildingId == 0)
            {
                result = await (from b in context.Buildings
                    join c in context.Customers on b.Id equals c.BuildingId into customerJoin
                    from c in customerJoin.DefaultIfEmpty()
                    join bf in context.BookFinancials on c.Id equals bf.CustomerId into financialJoin
                    from bf in financialJoin.DefaultIfEmpty()
                    where bf.InvoiceId == invoiceId.Value
                    select new BookFinancialInfoViewModel
                    {
                        Id = bf.Id,
                        BuildingName = b.Name,
                        CustomerInfo = c.CustomerInfo,
                        Status = bf.Status,
                        InvoiceId = bf.InvoiceId.Value,
                        Description = bf.Description,
                        DatumF = bf.DatumF.Value,
                        Owes = bf.Owes,
                        Demands = bf.Demands
                    }).ToListAsync();
            }
            else
            {
                result = await (from b in context.Buildings
                    join c in context.Customers on b.Id equals c.BuildingId into customerJoin
                    from c in customerJoin.DefaultIfEmpty()
                    join bf in context.BookFinancials on c.Id equals bf.CustomerId into financialJoin
                    from bf in financialJoin.DefaultIfEmpty()
                    where bf.InvoiceId == invoiceId.Value && b.Id == buildingId.Value
                    select new BookFinancialInfoViewModel
                    {
                        Id = bf.Id,
                        BuildingName = b.Name,
                        CustomerInfo = c.CustomerInfo,
                        Status = bf.Status,
                        InvoiceId = bf.InvoiceId.Value,
                        Description = bf.Description,
                        DatumF = bf.DatumF.Value,
                        Owes = bf.Owes,
                        Demands = bf.Demands
                    }).ToListAsync();
            }
            if (!string.IsNullOrEmpty(dateFrom) && !string.IsNullOrEmpty(dateTo))
            {
                DateFrom = DateOnly.ParseExact(dateFrom, "dd.MM.yyyy", null);
                DateTo = DateOnly.ParseExact(dateTo, "dd.MM.yyyy", null);
                ViewBag.DateFrom = DateFrom.ToString("dd.MM.yyyy");
                ViewBag.DateTo = DateTo.ToString("dd.MM.yyyy");
                result = result.Where(x =>
                    x.DatumF >= DateFrom && x.DatumF <= DateTo && (int)x.Status == paymentStatusId.Value).ToList();
            }
            else if (!string.IsNullOrEmpty(dateFrom))
            {
                DateFrom = DateOnly.ParseExact(dateFrom, "dd.MM.yyyy", null);
                ViewBag.DateFrom = DateFrom.ToString("dd.MM.yyyy");
                result = result.Where(x => x.DatumF >= DateFrom && (int)x.Status == paymentStatusId.Value)
                    .ToList();
            }
            else
            {
                result = result.Where(x => (int)x.Status == paymentStatusId.Value)
                    .ToList();
            }

            foreach (var doc in result.Where(x => x.Status == PaymentStatus.Неплатено || x.Status == PaymentStatus.Задоцнето))
            {
                var today = DateOnly.FromDateTime(DateTime.Now);
                var overdueDays = (today.DayNumber - doc.DatumF.DayNumber); // Calculate overdue days
                doc.Delay = (today.DayNumber - doc.DatumF.DayNumber); // DayNumber gives day difference directly
                if (doc.Delay > 30)
                {
                    doc.Status = PaymentStatus.Задоцнето;
                }
                else
                {
                    doc.Status = PaymentStatus.Неплатено;
                }
                // Determine the percentage based on overdue days
                double percentage = overdueDays switch
                {
                    < 0 => 0, // Not overdue
                    < 30 => 0.02, // 2%
                    >= 30 and <= 60 => 0.04, // 4%
                    >= 61 and <= 90 => 0.06, // 6%
                    >= 91 and <= 180 => 0.08, // 8%
                    >= 181 and <= 360 => 0.10, // 10%
                    >= 361 and <= 730 => 0.13, // 13%
                    _ => 0.16 // 16% for 730+ days
                };

                doc.NewTotal = (int)Math.Round(doc.Owes * (1 + percentage), MidpointRounding.AwayFromZero);
            }
            return View("Index", result) ;
        }
    }
}
