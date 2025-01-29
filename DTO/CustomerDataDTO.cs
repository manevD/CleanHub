namespace CleanHub.DTO
{
    public class CustomerDataDTO
    {
        public int NumberNalog { get; set; }
        public DateOnly Date { get; set; }
        public int Number { get; set; }
        public string Description { get; set; }
        public string DocumentTyp { get; set; }
        public double Owes { get; set; }
        public double Demands { get; set; }
    }
}
