namespace CleanHub.Models
{
    public class Building
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int? NumberOfResidence { get; set; }

        // Navigation property for residents
        public ICollection<Resident>? Residents { get; set; }
    }
}