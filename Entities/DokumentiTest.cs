using System.ComponentModel.DataAnnotations;

namespace CleanHub.Entities
{
    public class DokumentiTest
    {
        [Key]
        [StringLength(30)]
        public string? Dokid { get; set; }
        [StringLength(30)]
        public string? Broj { get; set; }
        [StringLength(20)]
        public string? Datum { get; set; }
        [StringLength(9)]
        public string? PartnerID { get; set; }
        [StringLength(7)]
        public string? Godina { get; set; }
        [StringLength(8)]
        public string? VkupnoIz { get; set; }
    }
}
