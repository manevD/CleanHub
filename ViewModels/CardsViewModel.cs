namespace CleanHub.ViewModels
{
    public class CardsViewModel
    {
        public List<BookFinancialViewModel> BuildingFinancial { get; set; }
        public List<BookFinancialViewModel> CustomerFinanfical { get; set; }
        public List<BuildingFinanceCardViewModel> BuildingFinanceCardViewModels { get; set; }

        public float CustomerOwesTotal { get; set; }
        public double CustomerDemandsTotal { get; set; }
        public double Total => CustomerDemandsTotal - CustomerOwesTotal;
    }
}
