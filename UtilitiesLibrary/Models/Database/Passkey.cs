using System.ComponentModel.DataAnnotations;

namespace UtilitiesLibrary.Models
{
    public class Passkey
    {
        [Key]
        public byte[] CredentialId { get; set; }
        public byte[] UserId { get; set; }
        public byte[] PublicKey { get; set; }
        public byte[] Challenge { get; set; }

        public int LoginDetailsId { get; set; }
        public int AlgorithmId { get; set; } // Expecting -7 (ES256) or -257 (RS256)

        public virtual LoginDetails LoginDetails { get; set; }
    }
}
