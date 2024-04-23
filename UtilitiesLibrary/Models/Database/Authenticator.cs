using System.ComponentModel.DataAnnotations;

namespace UtilitiesLibrary.Models
{
    public class Authenticator
    {
        [Key]
        public int AuthenticatorId { get; set; }
        public int LoginDetailsId { get; set; }
        public string Type { get; set; } // TOTP, HOTP, etc.
        public string Secret { get; set; }

        public DateTime LastUsedDate = DateTime.Now;

        public virtual LoginDetails LoginDetails { get; set; }
    }
}
