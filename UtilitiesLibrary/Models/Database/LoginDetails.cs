using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace UtilitiesLibrary.Models
{
    [Index(nameof(RootDomain))]
    public class LoginDetails
    {
        [Key]
        public int DetailsId { get; set; }
        public string RootDomain { get; set; }
        public string Username { get; set; }
        public byte[] Password { get; set; }
    }
}
