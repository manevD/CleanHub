namespace CleanHub.Entities
{
    public class BuildingProduct
    {
        public int Id { get; set; }

        public int BuildingId { get; set; } // Foreign Key
        public Building Building { get; set; } // Navigation property

        public int ProductId { get; set; } // Foreign Key
        public Product Product { get; set; } // Navigation property
    }
}
