using System.ComponentModel.DataAnnotations;

namespace UtilitiesLibrary.Models
{
    public class PinCode
    {
        [Key]
        public int Id { get; set; }
        public int Code { get; set; }

        public int LoginDetailsId { get; set; }
        public virtual LoginDetails LoginDetails { get; set; }
    }
}
