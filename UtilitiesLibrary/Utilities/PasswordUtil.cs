using Geralt;
using System.Text;

namespace UtilitiesLibrary.Utilities
{
    internal static class PasswordUtil
    {
        internal static byte[] ByteArrayFromSpan(ReadOnlySpan<byte> password)
        {
            byte[] passwordBytes = new byte[password.Length];
            password.CopyTo(passwordBytes);
            return passwordBytes;
        }

        internal static string PlainFromContainer(byte[] password)
        {
            return Encoding.UTF8.GetString(password);
        }

        internal static string PlainFromContainer(ReadOnlySpan<byte> password)
        {
            return Encoding.UTF8.GetString(password);
        }

        internal static byte[] EncryptPassword(byte[] password)
        {
            ReadOnlySpan<byte> sourcePassword = password;

            Span<byte> computedHash = stackalloc byte[64];
            Argon2id.ComputeHash(computedHash, sourcePassword, 3, 67108864); // RFC recommend parameters

            return ByteArrayFromSpan(computedHash);
        }
    }
}
