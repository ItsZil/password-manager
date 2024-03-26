using System.Text;

namespace UtilitiesLibrary.Utilities
{
    internal static class PasswordUtil
    {
        internal static byte[] ByteArrayFromPlain(string password)
        {
            return Encoding.UTF8.GetBytes(password);
        }

        internal static string PlainFromByteArray(byte[] password)
        {
            return Encoding.UTF8.GetString(password);
        }

        internal static async Task<byte[]> EncryptPassword(byte[] password)
        {
            // TODO: Implement encryption
            string sourcePassword = PlainFromByteArray(password);
            byte[] encryptedPassword = ByteArrayFromPlain(sourcePassword);
            return encryptedPassword;
        }
    }
}
