namespace CleanHub.Models
{
    public class Building
    {
        public int Id { get; set; }
        public Address Address { get; set; }
        public int NumberOfUnits { get; set; }

        // Navigation property for residents
        public ICollection<Resident> Residents { get; set; }
    }
}