using Microsoft.EntityFrameworkCore;

namespace UtilitiesLibrary.Models
{

    [PrimaryKey("MasterPasswordHash")]
    public class Configuration
    {
        public byte[] MasterPasswordHash { get; set; }
    }
}
