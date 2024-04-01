using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace UtilitiesLibrary.Models
{
    //[Index(nameof(Username))]
    public class User
    {
        [Key]
        public int UserId { get; set; }
        /*public string Username { get; set; }
        public byte[] MasterPasswordHash { get; set; }
        public byte[] Salt { get; set; }*/
        public byte[] EncryptionKey { get; set; }
    }
}
