namespace CleanHub.DTO
{
    public class CustomerDataDTO
    {
        public long Id { get; set; }
        public int NumberNalog { get; set; }
        public DateOnly Date { get; set; }
        public int Number { get; set; }
        public string Description { get; set; }
        public string DocumentTyp { get; set; }
        public double Owes { get; set; }
        public double Demands { get; set; }

        public bool DontSum { get; set; }
        public string Source { get; set; }
    }
}
