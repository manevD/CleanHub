using CleanHub.Attribute;
using CleanHub.Entities;
using CleanHub.Extensions;
using CleanHub.Infrastructure.Data;
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

        private List<BookFinancialInfoViewModel> GetFilteredBookFinancials(int? invoiceId, int buildingId)
        {
            var query = _unitOfWork.BookFinancials.GetBuldingReserve(buildingId, invoiceId ?? (int)InvoiceTyp.Reserve);

            return query.Select(bf => new BookFinancialInfoViewModel
            {
                Id = bf.Id,
                Status = bf.Status,
                InvoiceId = bf.InvoiceId ?? 0,
                Description = bf.Description ?? "",
                DocumentTypId = bf.DocumentTypId ?? 0,
                DatumF = bf.DatumF ?? DateOnly.MinValue,
                Owes = bf.Owes,
                Demands = bf.Demands,
                DontSum = bf.DontSum
            }).ToList();
        }

        [Route("Завршна")]
        [HttpGet]
        public async Task<IActionResult> LastInvoice()
        {
            ViewBag.PaymentStatusList = GetEnumSelectList<PaymentStatus>();
            ViewBag.Buildings = new SelectList(GetBuildings(), "Id", "Name");

            return View();
        }

        [Route("Завршна")]
        [HttpPost]
        public async Task<IActionResult> LastInvoice(int? buildingId, int? paymentStatusId, int year)
        {
            try
            {
                // Jahresanfang
                DateTime startDate = new DateTime(year, 1, 1);

                // Jahresende
                DateTime endDate = new DateTime(year, 12, 31, 23, 59, 59);
                string dateFromStr = startDate.ToString("dd.MM.yyyy");
                string dateToStr = endDate.ToString("dd.MM.yyyy");
                List<BookFinancialInfoViewModel> results = new List<BookFinancialInfoViewModel>();
                var building = new Building();
                if (!buildingId.HasValue)
                {
                    ViewBag.PaymentStatusList = GetEnumSelectList<PaymentStatus>();
                    return RedirectToAction(nameof(LastInvoice));
                }

                building = await _unitOfWork.Buildings.GetByIdAsync(x => x.Id == buildingId.Value);
                if (building != null)
                {
                    results = GetFilteredBookFinancials((int)InvoiceTyp.Reserve, buildingId.Value).ToList();
                    ViewBag.TotalDemands = GetDemandsLastInvoice(dateToStr, results);
                    ViewBag.TotalOwes = GetOwesLastInvoice(dateToStr, results);
                    FilterResultsByDate(ref results, dateFromStr ?? "", dateToStr ?? "", (int)InvoiceTyp.Reserve, paymentStatusId);
                    //if (paymentStatusId == (int)PaymentStatus.Неплатено || paymentStatusId == (int)PaymentStatus.Сите)
                    //{
                    //    CalculateOverdueStatus(results);
                    //}
                }
                else
                {
                    return RedirectToAction(nameof(LastInvoice));
                }
                ViewBag.PaymentStatusList = GetEnumSelectList<PaymentStatus>();
                ViewBag.Buildings = new SelectList(GetBuildings(), "Id", "Name", building.Id);
                ViewBag.SelectedBuildingName = building.Name;

                ViewBag.BuildingId = building.Id;
                return View(results.OrderBy(x => x.DatumF));
            }
            catch (Exception e)
            {
                throw;
            }
        }

        private double GetOwesLastInvoice(string? dateTo, List<BookFinancialInfoViewModel> results)
        {
            DateOnly? DateTo = null;
            if (!string.IsNullOrEmpty(dateTo))
            {
                DateTo = DateOnly.ParseExact(dateTo, "dd.MM.yyyy", null);
                return results
                    .Where(x =>
                        (!DateTo.HasValue || x.DatumF <= DateTo.Value) && x.Description != "салдо" &&
                        x.DocumentTypId != 11 && !string.IsNullOrEmpty(x.Description)).Sum(su => su.Owes);
            }
            return 0;
        }

        private double GetDemandsLastInvoice(string? dateTo, List<BookFinancialInfoViewModel> results)
        {
            DateOnly? DateTo = null;
            if (!string.IsNullOrEmpty(dateTo))
            {
                DateTo = DateOnly.ParseExact(dateTo, "dd.MM.yyyy", null);
                return results
                    .Where(x =>
                        (!DateTo.HasValue || x.DatumF <= DateTo.Value) && x.Description != "салдо" &&
                        x.DocumentTypId != 11).Sum(su => su.Demands);
            }
            return 0;
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

                building = await _unitOfWork.Buildings.GetByIdAsync(x => x.Id == buildingId.Value, inc => inc.Include(cs=> cs.Customers));
                if (building != null)
                {

                    results = GetFilteredBookFinancials(invoiceId, buildingId.Value).ToList();
                    var resultToAdd = new BookFinancialInfoViewModel();
                    var customer = await _unitOfWork.Customers.GetByIdAsync(x => x.Id == building.CustomerRefId);
                    foreach (var cust in building.Customers)
                    {
                        if (invoiceId == (int)InvoiceTyp.Reserve)
                        {
                            resultToAdd.InvoiceId = 1201;
                            resultToAdd.Description = "салдо";
                            resultToAdd.Owes = (cust?.Saldo1201 ?? 0) < 0 ? Math.Abs((double)(cust?.Saldo1201 ?? 0)) : 0;

                            resultToAdd.Demands = (cust?.Saldo1201 ?? 0) > 0 ? (double)(cust?.Saldo1201 ?? 0) : 0;
                            resultToAdd.DatumF = new DateOnly(2026, 1, 1);
                            results.Add(resultToAdd);
                        }
                        else
                        {
                            resultToAdd.InvoiceId = 1200;
                            resultToAdd.Description = "салдо";
                            resultToAdd.Owes = (cust?.Saldo ?? 0) < 0 ? Math.Abs((double)(cust?.Saldo ?? 0)) : 0;

                            resultToAdd.Demands = (cust?.Saldo ?? 0) > 0 ? (double)(cust?.Saldo ?? 0) : 0;
                            resultToAdd.DatumF = new DateOnly(2026, 1, 1);
                            results.Add(resultToAdd);
                        }
                    }
                    
                    ViewBag.TotalDemands = results.Where(x => !x.DontSum).Sum(su => su.Demands);
                    ViewBag.TotalOwes = results.Where(x => !x.DontSum).Sum(su => su.Owes);
                    FilterResultsByDate(ref results, dateFrom ?? "", dateTo ?? "", invoiceId ?? (int)InvoiceTyp.Reserve, paymentStatusId);
                    //if (paymentStatusId == (int)PaymentStatus.Неплатено || paymentStatusId == (int)PaymentStatus.Сите)
                    //{
                    //    CalculateOverdueStatus(results);
                    //}
            }
                ViewBag.PaymentStatusList = GetEnumSelectList<PaymentStatus>();
            ViewBag.InvoiceTypList = GetEnumSelectList<InvoiceTyp>().Where(x => x.Text != "Струја").ToList();
            ViewBag.Buildings = new SelectList(GetBuildings(), "Id", "Name", building.Id);
            ViewBag.SelectedBuildingName = building.Name;
            ViewBag.BuildingId = building.Id;
            return View("Index", results.OrderBy(x => x.DatumF));
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

//private void CalculateOverdueStatus(List<BookFinancialInfoViewModel> results)
//{
//    var today = DateOnly.FromDateTime(DateTime.Now);
//    var resultFiltered =
//        results.Where(x => x.Status == PaymentStatus.Неплатено || x.Status == PaymentStatus.Задоцнето);
//    foreach (var doc in resultFiltered)
//    {
//        doc.Delay = today.DayNumber - doc.DatumF.DayNumber;
//        doc.Status = doc.Delay > 30 ? PaymentStatus.Задоцнето : PaymentStatus.Неплатено;

//        double percentage = doc.Delay switch
//        {
//            < 0 => 0,
//            < 30 => 0.02,
//            <= 60 => 0.04,
//            <= 90 => 0.06,
//            <= 180 => 0.08,
//            <= 360 => 0.10,
//            <= 730 => 0.13,
//            _ => 0.16
//        };
//        doc.NewTotal = (int)Math.Round(doc.Owes * (1 + percentage), MidpointRounding.AwayFromZero);
//    }
//}

[Route("Trosoci")]
[HttpGet]
public async Task<IActionResult> Costs()
{
    ViewBag.PaymentStatusList = GetEnumSelectList<PaymentStatus>();
    ViewBag.Buildings = new SelectList(GetBuildings(), "Id", "Name");
    return View();
}
public async Task<IActionResult> DeleteCosts(int id)
{
    var cost = await _unitOfWork.BookFinancials.GetByIdAsync(x => x.Id == id);
    if (cost != null)
    {
        _unitOfWork.BookFinancials.Delete(cost);
        await _unitOfWork.SaveChangesAsync();
    }

    return RedirectToAction(nameof(Costs));
}

[Route("Trosoci")]
[HttpPost]
public async Task<IActionResult> Costs(int? buildingId, int? paymentStatusId, string? dateFrom, string? dateTo)
{
    List<BookFinancialInfoViewModel> results = new List<BookFinancialInfoViewModel>();
    var building = new Building();
    if (!buildingId.HasValue)
    {
        ViewBag.PaymentStatusList = GetEnumSelectList<PaymentStatus>();
        return RedirectToAction(nameof(Costs));
    }

    building = await _unitOfWork.Buildings.GetByIdAsync(x => x.Id == buildingId.Value);
    if (building != null)
    {
        results = GetFilteredBookFinancials((int)InvoiceTyp.Reserve, buildingId.Value).ToList();
        FilterResultsByDate(ref results, dateFrom ?? "", dateTo ?? "", (int)InvoiceTyp.Reserve, paymentStatusId);
        results = results.Where(x => x.Owes != 0).ToList();
    }
    else
    {
        return RedirectToAction(nameof(Costs));
    }
    ViewBag.PaymentStatusList = GetEnumSelectList<PaymentStatus>();
    ViewBag.Buildings = new SelectList(GetBuildings(), "Id", "Name", building.Id);
    ViewBag.SelectedBuildingName = building.Name;
    if (!string.IsNullOrWhiteSpace(dateFrom))
    {
        if (DateOnly.TryParseExact(dateFrom, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out var parsedFrom))
        {
            ViewBag.DateFrom = parsedFrom;
        }
    }

    if (!string.IsNullOrWhiteSpace(dateTo))
    {
        if (DateOnly.TryParseExact(dateTo, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out var parsedTo))
        {
            ViewBag.DateTo = parsedTo;
        }
    }
    ViewBag.BuildingId = building.Id;
    return View(results.OrderBy(x => x.DatumF));
}

[HttpGet]
public async Task<IActionResult> EditCosts(long? id)
{
    var cost = await _unitOfWork.BookFinancials
.GetByIdAsync(x => x.Id == id);

    if (cost == null)
        return NotFound();


    return PartialView("_EditCostModal", cost);
}

// POST: customers/Edit/5
// To protect from overposting attacks, enable the specific properties you want to bind to.
// For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> EditCosts(BookFinancial model)
{
    if (model.Id == null || model.Id == 0)
        return PartialView("_EditCostModal", model);

    var cost = await _unitOfWork.BookFinancials.GetByIdAsync(x => x.Id == model.Id);
    if (cost == null)
        return NotFound();

    cost.Description = model.Description;
    cost.Owes = model.Owes;

    await _unitOfWork.SaveChangesAsync();

    return Json(new { success = true });
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
