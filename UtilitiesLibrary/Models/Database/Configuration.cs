using Microsoft.EntityFrameworkCore;

namespace UtilitiesLibrary.Models
{

    [PrimaryKey("MasterPasswordHash")]
    public class Configuration
    {
        public byte[] MasterPasswordHash { get; set; }
        public byte[] Salt { get; set; }
        public byte[] VaultEncryptionKey { get; set; }
    }
}
