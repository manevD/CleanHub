namespace CleanHub.ViewModels
{
    public class BuildingsCardViewModel
    {
        public string Name { get; set; }
        public double PayedMoney { get; set; }
        public double ReserveFund { get; set; }
        public double Cost { get; set; }


        //public int PayedMoneyTotal { get; set; }
        //public int ReserveFundTotal { get; set; }

        //public int PayedMoneyTotalMinusReserveFundTotal => PayedMoneyTotal - ReserveFundTotal;
        //public int CostTotal { get; set; }
        //public int Total => ReserveFundTotal - CostTotal;

    }
}
