using Microsoft.EntityFrameworkCore;

namespace UtilitiesLibrary.Models
{
    [PrimaryKey("Salt")]
    public class Configuration
    {
        public byte[] Salt { get; set; }
        public byte[] VaultEncryptionKey { get; set; }
    }
}
