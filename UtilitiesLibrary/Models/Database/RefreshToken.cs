using System.ComponentModel.DataAnnotations;

namespace UtilitiesLibrary.Models
{
    public class RefreshToken
    {
        [Key]
        public int TokenId { get; set; }
        public string Token { get; set; }
        public DateTime ExpiryDate { get; set; }
    }
}
