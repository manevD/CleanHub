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
    public class BookFinancialController : Controller
    {
        private static DateOnly DateFrom = DateOnly.FromDateTime(DateTime.Now);
        private static DateOnly DateTo = DateOnly.FromDateTime(DateTime.Now);

        private readonly IUnitOfWork _unitOfWork;

        public BookFinancialController( IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        private List<Building> GetBuildings()
        {
            var buildings =  _unitOfWork.Buildings.GetAll().ToList();
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

        private List<BookFinancialInfoViewModel> GetFilteredBookFinancials(int? invoiceId, int buildingId, int? customerRefId, int? status)
        {
            var query = _unitOfWork.BookFinancials.GetBuldingReserve(buildingId, invoiceId ?? 1201, status ?? 0);
            return query.Select(bf => new BookFinancialInfoViewModel
                {
                    Id = bf.Id,
                    Status = bf.Status,
                    InvoiceId = bf.InvoiceId ?? 0,
                    Description = bf.Description ?? "",
                    DatumF = bf.DatumF ?? DateOnly.MinValue,
                    Owes = bf.Owes,
                    Demands = bf.Demands
                })
                .ToList();
        }

        public IActionResult Index()
        {
            ViewBag.PaymentStatusList = GetEnumSelectList<PaymentStatus>();
            ViewBag.InvoiceTypList = GetEnumSelectList<InvoiceTyp>().Where(x => x.Text != "Струја").ToList();
            ViewBag.Buildings = new SelectList(GetBuildings(), "Id", "Name");

            return View("Index");
        }
        //select sum(Owes)  from BookFinancials where CustomerId = 781 and InvoiceId = 1201 Dolzi
        //select sum(demands) from BookFinancials where CustomerId in (select id from Customers where BuildingId = 80) and InvoiceId = 1201 and DatumF >='01.12.2022' Pobaruva
        public async Task<IActionResult> Books(int? invoiceId, int? buildingId, int? paymentStatusId, string? dateFrom, string? dateTo)
        {
            List<BookFinancialInfoViewModel> results = new List<BookFinancialInfoViewModel>();
            var building = new Building();
            if (!buildingId.HasValue)
            {
                ViewBag.PaymentStatusList = GetEnumSelectList<PaymentStatus>();
                ViewBag.InvoiceTypList = GetEnumSelectList<InvoiceTyp>().Where(x => x.Text != "Струја").ToList();
                ViewBag.Buildings = new SelectList(GetBuildings(), "Id", "Name", buildingId);
                return RedirectToAction(nameof(Index));
            }

            building = await _unitOfWork.Buildings.GetByIdAsync(x => x.Id == buildingId.Value);
            if (building != null)
            {
                results = GetFilteredBookFinancials(invoiceId, buildingId.Value, building.CustomerRefId ?? 0, paymentStatusId ?? 0).ToList();
                FilterResultsByDate(ref results, dateFrom ?? "", dateTo ?? "", invoiceId ?? 1201, paymentStatusId);
                if (paymentStatusId == (int)PaymentStatus.Неплатено)
                {
                    CalculateOverdueStatus(results);
                }
            }

            ViewBag.PaymentStatusList = GetEnumSelectList<PaymentStatus>();
            ViewBag.InvoiceTypList = GetEnumSelectList<InvoiceTyp>().Where(x => x.Text != "Струја").ToList();
            ViewBag.Buildings = new SelectList(GetBuildings(), "Id", "Name", buildingId);
            return View("Index",results );
        }
        private void FilterResultsByDate(ref List<BookFinancialInfoViewModel> results, string dateFrom, string dateTo, int invoiceId, int? paymentStatusId)
        {
            if (string.IsNullOrEmpty(dateFrom))
                return;

            var DateFrom = DateOnly.ParseExact(dateFrom, "dd.MM.yyyy", null);
            ViewBag.DateFrom = DateFrom;

            if (!string.IsNullOrEmpty(dateTo))
            {
                var DateTo = DateOnly.ParseExact(dateTo, "dd.MM.yyyy", null);
                ViewBag.DateTo = DateTo;

                results = results.Where(x =>
                        (x.DatumF >= DateFrom && x.DatumF <= DateTo) &&
                        (invoiceId == (int)InvoiceTyp.Reserve || !paymentStatusId.HasValue || (int)x.Status == paymentStatusId))
                    .ToList();
            }
            else
            {
                results = results.Where(x =>
                        (x.DatumF >= DateFrom) &&
                        (invoiceId == (int)InvoiceTyp.Reserve || !paymentStatusId.HasValue || (int)x.Status == paymentStatusId))
                    .ToList();
            }
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

            var bookFinancialToUpdate = await _unitOfWork.BookFinancials.GetByIdAsync(x => x.DocumentId == id);
            if (bookFinancialToUpdate == null) return NotFound();

            try
            {
                bookFinancialToUpdate.Status = PaymentStatus.Платено;
                _unitOfWork.BookFinancials.Update(bookFinancialToUpdate);
                await _unitOfWork.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_unitOfWork.BookFinancials.GetAll().Any(x => x.DocumentId == id)) return NotFound();
                throw;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
