using AutoMapper.Configuration.Annotations;
using System.ComponentModel.DataAnnotations;

namespace CleanHub.ViewModels
{
    public class BookViewModel
    {
        public int Id { get; set; }
        public int DocId { get; set; }
        public DocumentViewModel Document { get; set; }
        public bool Hide { get; set; }
        public int? ArticleId { get; set; }

        public float? Input { get; set; }

        public float? Output { get; set; }

        public float? Quantity { get; set; }

        public float? Price { get; set; }

        public float? PriceWithTax { get; set; }

        public float? Tax { get; set; }

        public float? Total { get; set; }

        public string? ArticleNotes { get; set; }

        public string? UnitOfMeasurement { get; set; }
        [DisplayFormat(DataFormatString = "{0.00}")]
        [Ignore]
        public float? PriceWithTaxTotal { get; set; }
    }
}
