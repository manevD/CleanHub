namespace CleanHub.Entities;

public partial class Building
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? BankAccount { get; set; }
    public int? ReserveFund { get; set; }
    public ICollection<Customer> Customers { get; set; }
    public ICollection<BuildingProduct> BuildingProducts { get; set; } = new List<BuildingProduct>();
}