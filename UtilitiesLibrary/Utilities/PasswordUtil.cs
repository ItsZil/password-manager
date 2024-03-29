using Geralt;
using System.Security.Cryptography;
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

        /// <summary>
        /// Hashes the vault master password using Argon2id
        /// </summary>
        /// <param name="sourcePassword">The master password to be hashed</param>
        /// <returns>The computed hash of the master password as a byte array</returns>
        internal static byte[] HashMasterPassword(ReadOnlySpan<byte> sourcePassword)
        {
            Span<byte> computedHash = stackalloc byte[64];
            Argon2id.ComputeHash(computedHash, sourcePassword, 3, 67108864); // RFC recommend parameters

            return ByteArrayFromSpan(computedHash);
        }

        /// <summary>
        /// Encrypts a password using AES encryption with a provided master password hash
        /// </summary>
        /// <param name="masterPasswordHash">The hash of the master password used as the encryption key</param>
        /// <param name="password">The password to be encrypted</param>
        /// <returns>The encrypted password as a byte array</returns>
        internal static byte[] EncryptPassword(byte[] masterPasswordHash, byte[] password)
        {
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = masterPasswordHash;
                aesAlg.GenerateIV();

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV), CryptoStreamMode.Write))
                    {
                        csEncrypt.Write(password, 0, password.Length);
                        csEncrypt.FlushFinalBlock();
                    }
                    byte[] encryptedPassword = msEncrypt.ToArray();
                    byte[] iv = aesAlg.IV;

                    byte[] result = new byte[iv.Length + encryptedPassword.Length];
                    Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
                    Buffer.BlockCopy(encryptedPassword, 0, result, iv.Length, encryptedPassword.Length);

                    return result;
                }
            }
        }
    }
}