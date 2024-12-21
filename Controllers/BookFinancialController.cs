using CleanHub.Attribute;
using CleanHub.CleanHub.Infrastructure.Data;
using CleanHub.Entities;
using CleanHub.Extensions;
using CleanHub.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PaymentStatus = CleanHub.Entities.Enums.PaymentStatus;

namespace CleanHub.Controllers
{
    [RequireLogin]
    public class BookFinancialController(ApplicationDbContext context) : Controller
    {
        private static DateOnly DateFrom = DateOnly.FromDateTime(DateTime.Now);
        private static DateOnly DateTo = DateOnly.FromDateTime(DateTime.Now);

        private List<Building> GetBuildings()
        {
            var buildings = context.Buildings.ToList();
            buildings.Insert(0, new Building { Name = "Сите", Id = 0 });
            return buildings;
        }

        private List<SelectListItem> GetEnumSelectList<TEnum>() where TEnum : Enum =>
            Enum.GetValues(typeof(TEnum))
                .Cast<TEnum>()
                .Select(e => new SelectListItem
                {
                    Text = e.GetEnumDescription(),
                    Value = ((int)(object)e).ToString(),
                }).ToList();

        private IQueryable<BookFinancialInfoViewModel> GetFilteredBookFinancials(int? invoiceId, int? buildingId)
        {
            var query = from b in context.Buildings
                        join c in context.Customers on b.Id equals c.BuildingId into customerJoin
                        from c in customerJoin.DefaultIfEmpty()
                        join bf in context.BookFinancials on c.Id equals bf.CustomerId into financialJoin
                        from bf in financialJoin.DefaultIfEmpty()
                        where (invoiceId == null || bf.InvoiceId == invoiceId) &&
                              (buildingId == null || buildingId == 0 || b.Id == buildingId)
                        select new BookFinancialInfoViewModel
                        {
                            Id = bf.Id,
                            BuildingName = b.Name,
                            CustomerInfo = c.CustomerInfo,
                            Status = bf.Status,
                            InvoiceId = bf.InvoiceId ?? 0,
                            Description = bf.Description,
                            DatumF = bf.DatumF ?? DateOnly.MinValue,
                            Owes = bf.Owes,
                            Demands = bf.Demands
                        };
            return query;
        }

        public IActionResult Index()
        {
            ViewBag.PaymentStatusList = GetEnumSelectList<PaymentStatus>();
            ViewBag.InvoiceTypList = GetEnumSelectList<InvoiceTyp>().Where(x => x.Text != "Струја").ToList();
            ViewBag.Buildings = new SelectList(GetBuildings(), "Id", "Name");

            return View("Index");
        }

        public async Task<IActionResult> Books(int? invoiceId, int? buildingId, int? paymentStatusId, string dateFrom, string dateTo)
        {
            var results = await GetFilteredBookFinancials(invoiceId, buildingId).ToListAsync();

            if (!string.IsNullOrEmpty(dateFrom)) DateFrom = DateOnly.ParseExact(dateFrom, "dd.MM.yyyy", null);
            if (!string.IsNullOrEmpty(dateTo)) DateTo = DateOnly.ParseExact(dateTo, "dd.MM.yyyy", null);

            results = results
                .Where(x => (x.DatumF >= DateFrom && x.DatumF <= DateTo) &&
                            (!paymentStatusId.HasValue || (int)x.Status == paymentStatusId))
                .ToList();

            CalculateOverdueStatus(results);

            ViewBag.PaymentStatusList = GetEnumSelectList<PaymentStatus>();
            ViewBag.InvoiceTypList = GetEnumSelectList<InvoiceTyp>().Where(x => x.Text != "Струја").ToList();
            ViewBag.Buildings = new SelectList(GetBuildings(), "Id", "Name", buildingId);
            ViewBag.DateFrom = DateFrom.ToString("dd.MM.yyyy");
            ViewBag.DateTo = DateTo.ToString("dd.MM.yyyy");

            return View("Index", results);
        }

        private void CalculateOverdueStatus(List<BookFinancialInfoViewModel> results)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);

            foreach (var doc in results)
            {
                if (doc.Status is not (PaymentStatus.Неплатено or PaymentStatus.Задоцнето))
                    continue;

                doc.Delay = today.DayNumber - doc.DatumF.DayNumber;
                doc.Status = doc.Delay > 30 ? PaymentStatus.Задоцнето : PaymentStatus.Неплатено;

                double percentage = doc.Delay switch
                {
                    < 0 => 0,
                    < 30 => 0.02,
                    <= 60 => 0.04,
                    <= 90 => 0.06,
                    <= 180 => 0.08,
                    <= 360 => 0.10,
                    <= 730 => 0.13,
                    _ => 0.16
                };

                doc.NewTotal = (int)Math.Round(doc.Owes * (1 + percentage), MidpointRounding.AwayFromZero);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetStatusPayment(int? id)
        {
            if (id == null || id == 0) return NotFound();

            var bookFinancialToUpdate = await context.BookFinancials.FirstOrDefaultAsync(x => x.DocumentId == id);
            if (bookFinancialToUpdate == null) return NotFound();

            try
            {
                bookFinancialToUpdate.Status = PaymentStatus.Платено;
                context.Update(bookFinancialToUpdate);
                await context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!context.BookFinancials.Any(x => x.DocumentId == id)) return NotFound();
                throw;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
