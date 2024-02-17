using System.ComponentModel.DataAnnotations;

namespace Server.Models
{
    public class Authenticator
    {
        [Key]
        public int AuthenticatorId { get; set; }
        public int UserId { get; set; }
        public string Type { get; set; } // TOTP, HOTP, etc.
        public string Secret { get; set; }

        public virtual User User { get; set; }
    }
}
