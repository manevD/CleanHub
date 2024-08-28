using CleanHub.Entities;

namespace CleanHub.ViewModels
{
    public class BuildingProductViewModel
    {
        public int Id { get; set; }
        public int BuildingId { get; set; } // Foreign Key
        public BuildingViewModel Building { get; set; } // Navigation property

        public int ProductId { get; set; } // Foreign Key
        public ProductViewModel Product { get; set; } // Navigation property
    }
}
