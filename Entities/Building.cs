using CleanHub.ViewModels;

namespace CleanHub.Entities;

public partial class Building
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public string? BankAccount { get; set; }
    public List<Customer> Customers { get; set; }


    //public int? OddelPartnerId { get; set; }
}