using System.ComponentModel.DataAnnotations;

namespace CleanHub.Entities
{
    public class PartneriTest
    {
        [Key]
        public int PartnerID { get; set; }

        [StringLength(50)]
        public string? Partner { get; set; }

        [StringLength(50)]
        public string? parAdresa { get; set; }
    }
}
