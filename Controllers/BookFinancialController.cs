using CleanHub.Attribute;
using CleanHub.Infrastructure.Data;
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
    public class BookFinancialController(IUnitOfWork _unitOfWork) : Controller
    {
        private static DateOnly DateFrom = DateOnly.FromDateTime(DateTime.Now);
        private static DateOnly DateTo = DateOnly.FromDateTime(DateTime.Now);

        private List<Building> GetBuildings()
        {
            var buildings = _unitOfWork.Buildings.GetAll().ToList();
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

        private List<BookFinancialInfoViewModel> GetFilteredBookFinancials(int? invoiceId, int buildingId, int? customerRefId, int? paymentStatusId)
        {
            var query = _unitOfWork.BookFinancials.GetBuldingReserve(buildingId, invoiceId ?? (int)InvoiceTyp.Reserve);

            
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
            try
            {
                List<BookFinancialInfoViewModel> results = new List<BookFinancialInfoViewModel>();
                var building = new Building();
                if (!buildingId.HasValue)
                {
                    ViewBag.PaymentStatusList = GetEnumSelectList<PaymentStatus>();
                    ViewBag.InvoiceTypList = GetEnumSelectList<InvoiceTyp>().Where(x => x.Text != "Струја").ToList();
                    return RedirectToAction(nameof(Index));
                }

                building = await _unitOfWork.Buildings.GetByIdAsync(x => x.Id == buildingId.Value);
                if (building != null)
                {
                    results = GetFilteredBookFinancials(invoiceId, buildingId.Value, building.CustomerRefId ?? 0, paymentStatusId ?? 0).ToList();
                    FilterResultsByDate(ref results, dateFrom ?? "", dateTo ?? "", invoiceId ?? (int)InvoiceTyp.Reserve, paymentStatusId);
                    if (paymentStatusId == (int)PaymentStatus.Неплатено || paymentStatusId == (int)PaymentStatus.Сите)
                    {
                        CalculateOverdueStatus(results);
                    }
                }
                ViewBag.PaymentStatusList = GetEnumSelectList<PaymentStatus>();
                ViewBag.InvoiceTypList = GetEnumSelectList<InvoiceTyp>().Where(x => x.Text != "Струја").ToList();
                ViewBag.Buildings = new SelectList(GetBuildings(), "Id", "Name", building.Id);
                ViewBag.SelectedBuildingName = building.Name;
                ViewBag.BuildingId = building.Id; 
                return View("Index", results.OrderBy(x=>x.DatumF));
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        private void FilterResultsByDate(ref List<BookFinancialInfoViewModel> results, string dateFrom, string dateTo, int invoiceId, int? paymentStatusId)
        {
            DateOnly? DateFrom = null;
            DateOnly? DateTo = null;

            if (!string.IsNullOrEmpty(dateFrom))
            {
                DateFrom = DateOnly.ParseExact(dateFrom, "dd.MM.yyyy", null);
                ViewBag.DateFrom = DateFrom;
            }

            if (!string.IsNullOrEmpty(dateTo))
            {
                DateTo = DateOnly.ParseExact(dateTo, "dd.MM.yyyy", null);
                ViewBag.DateTo = DateTo;
            }

            if (invoiceId == (int)InvoiceTyp.Reserve)
            {
                results = results
                    .Where(x =>
                        (!DateFrom.HasValue || x.DatumF >= DateFrom.Value) &&
                        (!DateTo.HasValue || x.DatumF <= DateTo.Value)).ToList();
            }
            else
            {
                results = results
                    .Where(x =>
                        (!DateFrom.HasValue || x.DatumF >= DateFrom.Value) &&
                        (!DateTo.HasValue || x.DatumF <= DateTo.Value) &&
                        (!paymentStatusId.HasValue || paymentStatusId == (int)PaymentStatus.Сите || (int)x.Status == paymentStatusId))
                    .ToList();
            }
          
        }

        private void CalculateOverdueStatus(List<BookFinancialInfoViewModel> results)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            var resultFiltered =
                results.Where(x => x.Status == PaymentStatus.Неплатено || x.Status == PaymentStatus.Задоцнето);
            foreach (var doc in resultFiltered)
            {
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
