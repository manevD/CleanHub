namespace CleanHub.Entities
{
    public class BuildingProduct
    {
        public int Id { get; set; }

        public int BuildingId { get; set; } // Foreign Key

        public float? Input { get; set; }

        public float? Output { get; set; }

        public float? Quantity { get; set; }

        public float? PriceWithTax { get; set; }
        public float Price { get; set; }

        public float? Tax { get; set; }

        public float? Total { get; set; }

        public string? ArticleNotes { get; set; }

        public string? UnitOfMeasurement { get; set; }
    }
}
