using Geralt;
using System.Security.Cryptography;
using System.Text;

namespace Server.Utilities
{
    internal static class PasswordUtil
    {
        internal static byte[] ByteArrayFromSpan(ReadOnlySpan<byte> password)
        {
            byte[] passwordBytes = new byte[password.Length];
            password.CopyTo(passwordBytes);
            return passwordBytes;
        }

        internal static byte[] ByteArrayFromPlain(string password)
        {
            return Encoding.UTF8.GetBytes(password);
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
        /// Generates a secure, random string to be used as a password
        /// </summary>
        /// <returns>A plain byte array of the password</returns>
        internal static byte[] GenerateSecurePassword(int length = 64)
        {
            string randomPassword = SecureRandom.GetString(length);

            return ByteArrayFromPlain(randomPassword);
        }

        /// <summary>
        /// Generates a secure, random passphrase of the specified word count
        /// </summary>
        /// <param name="wordCount">The amount of words the passphrase should consist of</param>
        /// <returns>A plain byte array of the passphrase</returns>
        internal static byte[] GeneratePassphrase(int wordCount)
        {
            char[] passphrasePlainChars = SecureRandom.GetPassphrase(wordCount, ' ');
            string passphrasePlain = new string(passphrasePlainChars);

            return ByteArrayFromPlain(passphrasePlain);
        }

        /// <summary>
        /// Hashes the vault master password using Argon2id
        /// </summary>
        /// <param name="sourcePassword">The master password to be hashed</param>
        /// <returns>The computed hash of the master password as a byte array</returns>
        internal static byte[] HashMasterPassword(ReadOnlySpan<byte> sourcePassword)
        {
            Span<byte> computedHash = stackalloc byte[128];
            Argon2id.ComputeHash(computedHash, sourcePassword, 3, 268435456);

            return ByteArrayFromSpan(computedHash);
        }

        /// <summary>
        /// Derives an encryption key from a plain password using Argon2id
        /// This key is only used for long-term password storage in the vault
        /// </summary>
        /// <param name="sourcePassword">The master password to derive encryption key from</param>
        /// <returns>A key to use for encrypting passwords before storage in the vault</returns>
        internal static byte[] DeriveEncryptionKeyFromMasterPassword(ReadOnlySpan<byte> sourcePassword, ref Span<byte> salt)
        {
            Span<byte> encryptionKey = stackalloc byte[32];
            Argon2id.DeriveKey(encryptionKey, sourcePassword, salt, 3, 67108864); // todo: increase

            return ByteArrayFromSpan(encryptionKey);
        }

        /// <summary>
        /// Encrypts a password using AES encryption with a provided encryption key
        /// </summary>
        /// <param name="encryptionKey">The encryption key</param>
        /// <param name="password">The password to be encrypted as a plain-text byte array</param>
        /// <returns>The encrypted password as a byte array</returns>
        internal static byte[] EncryptPassword(byte[] encryptionKey, byte[] password)
        {
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = encryptionKey;
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

        /// <summary>
        /// Decrypts a password using AES encryption a provided encryption key
        /// </summary>
        /// <param name="encryptionKey">The encryption key</param>
        /// <param name="password">The password to be decrypted as an encrypted byte array</param>
        /// <returns>The encrypted password as a byte array</returns>
        internal static byte[] DecryptPassword(byte[] encryptionKey, byte[] password)
        {
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = encryptionKey;

                // Extract IV from the password
                byte[] iv = new byte[aesAlg.BlockSize / 8];
                Buffer.BlockCopy(password, 0, iv, 0, iv.Length);

                // Remove IV from the password
                byte[] encryptedPassword = new byte[password.Length - iv.Length];
                Buffer.BlockCopy(password, iv.Length, encryptedPassword, 0, encryptedPassword.Length);

                aesAlg.IV = iv;

                try
                {
                    using (MemoryStream msDecrypt = new MemoryStream(encryptedPassword))
                    {
                        using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV), CryptoStreamMode.Read))
                        {
                            using (MemoryStream decryptedData = new MemoryStream())
                            {
                                    int bytesRead;
                                    byte[] buffer = new byte[1024];

                                    while ((bytesRead = csDecrypt.Read(buffer, 0, buffer.Length)) > 0)
                                    {
                                        decryptedData.Write(buffer, 0, bytesRead);
                                    }

                                    return decryptedData.ToArray();
                                }

                            }
                        }
                    }
                catch (CryptographicException e)
                {
                    Console.WriteLine(e);
                    return Array.Empty<byte>();
                }
            }
        }
    }
}