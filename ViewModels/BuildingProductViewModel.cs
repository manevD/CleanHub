using System.ComponentModel.DataAnnotations;
using AutoMapper.Configuration.Annotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;

namespace CleanHub.ViewModels
{
    public class BuildingProductViewModel 
    {
        public int Id { get; set; }
        public int BuildingId { get; set; } // Foreign Key
        public bool GetFromReserve { get; set; } = false;
        [DisplayFormat(DataFormatString = "{0.00}")]
        public float? Input { get; set; }
        [DisplayFormat(DataFormatString = "{0.00}")]
        public float? Output { get; set; }

        public float? Quantity { get; set; }

        [Display(Name = "Price with Tax")]
        [DisplayFormat(DataFormatString = "{0.00}")]
        public float? PriceWithTax { get; set; }

        [DisplayFormat(DataFormatString = "{0.00}")]
        public float Price { get; set; }

        [DisplayFormat(DataFormatString = "{0.00}")]
        public float? Tax { get; set; }

        [DisplayFormat(DataFormatString = "{0.00}")]
        public float? Total { get; set; }

        public string? ArticleNotes { get; set; }
        [DisplayFormat(DataFormatString = "{0.00}")]
        [Ignore]
        public float? PriceWithTaxTotal { get; set; }
        public string? UnitOfMeasurement { get; set; }
    }
}
