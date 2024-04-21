using System.ComponentModel.DataAnnotations;

namespace UtilitiesLibrary.Models
{
    public class LoginDetails
    {
        [Key]
        public int Id { get; set; }
        public string RootDomain { get; set; }
        public string Username { get; set; }
        public byte[] Password { get; set; }
        public byte[] Salt { get; set; }
        public DateTime LastUsedDate { get; set; } = DateTime.Now;

        public int ExtraAuthId { get; set; } = 1;
        public virtual ExtraAuth ExtraAuth { get; set; }
    }
}
