using System.ComponentModel.DataAnnotations;

namespace CleanHub.ViewModels
{
    public class BuildingProductViewModel
    {
        public int Id { get; set; }
        public int BuildingId { get; set; } // Foreign Key

        public float? Input { get; set; }

        public float? Output { get; set; }

        public float? Quantity { get; set; }

        [Display(Name = "Price with Tax")]
        [DataType(DataType.Currency)]
        [DisplayFormat(DataFormatString = "{0:C2}", ApplyFormatInEditMode = true)]
        public float? PriceWithTax { get; set; }
        public float Price { get; set; }

        public float? Tax { get; set; }

        public float? Total { get; set; }

        public string? ArticleNotes { get; set; }

        public string? UnitOfMeasurement { get; set; }
    }
}
