using System.ComponentModel.DataAnnotations;

namespace UtilitiesLibrary.Models
{
    public class ExtraAuth
    {
        [Key]
        public int Id { get; set; }
        public string Type { get; set; }
    }
}
