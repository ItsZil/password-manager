using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace UtilitiesLibrary.Models
{
    [Index(nameof(ServiceRootName))]
    public class Account
    {
        [Key]
        public int AccountId { get; set; }
        public int UserId { get; set; }
        public string ServiceRootName { get; set; }
        public string Username { get; set; }
        public byte[] Password { get; set; }

        public virtual User User { get; set; }
    }
}
