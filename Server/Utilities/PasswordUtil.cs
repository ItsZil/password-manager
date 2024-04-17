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
        internal static byte[] DeriveEncryptionKeyFromPassword(ReadOnlySpan<byte> sourcePassword, ref Span<byte> salt)
        {
            Span<byte> encryptionKey = stackalloc byte[32];
            SecureRandom.Fill(salt);

            Argon2id.DeriveKey(encryptionKey, sourcePassword, salt, 3, 67108864);

            return ByteArrayFromSpan(encryptionKey);
        }

        internal static byte[] DeriveEncryptionKeyFromPassword(ReadOnlySpan<byte> sourcePassword, Span<byte> salt)
        {
            Span<byte> encryptionKey = stackalloc byte[32];

            Argon2id.DeriveKey(encryptionKey, sourcePassword, salt, 3, 67108864);

            return ByteArrayFromSpan(encryptionKey);
        }

        internal async static Task<(byte[], byte[])> EncryptPassword(byte[] sourcePassword, byte[] pragmaKey)
        {
            // Generate a random salt
            byte[] salt = new byte[16];
            SecureRandom.Fill(salt);

            // Derive an encryption key from the master password's hash
            byte[] encryptionKey = DeriveEncryptionKeyFromPassword(pragmaKey, salt);

            // Encrypt the password using the derived key
            byte[] encryptedPassword = await EncryptMessage(encryptionKey, sourcePassword);

            return (encryptedPassword, salt);
        }

        internal async static Task<byte[]> DecryptPassword(byte[] sourcePasswordHash, byte[] salt, byte[] pragmaKey)
        {
            // Derive an encryption key from the master password's hash
            byte[] encryptionKey = DeriveEncryptionKeyFromPassword(pragmaKey, salt);

            // Decrypt the password using the derived key
            byte[] decryptedPassword = await DecryptMessage(encryptionKey, sourcePasswordHash);

            return decryptedPassword;
        }

        /// <summary>
        /// Encrypts a message using AES encryption with a provided encryption key
        /// </summary>
        /// <param name="encryptionKey">The encryption key</param>
        /// <param name="message">The message to be encrypted as a plain-text byte array</param>
        /// <returns>The encrypted message as a byte array</returns>
        internal async static Task<byte[]> EncryptMessage(byte[] encryptionKey, byte[] message)
        {
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = encryptionKey;
                aesAlg.GenerateIV();

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV), CryptoStreamMode.Write))
                    {
                        await csEncrypt.WriteAsync(message, 0, message.Length);
                        csEncrypt.FlushFinalBlock();
                    }
                    byte[] encryptedMessage = msEncrypt.ToArray();
                    byte[] iv = aesAlg.IV;

                    byte[] result = new byte[iv.Length + encryptedMessage.Length];
                    Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
                    Buffer.BlockCopy(encryptedMessage, 0, result, iv.Length, encryptedMessage.Length);

                    return result;
                }
            }
        }

        /// <summary>
        /// Decrypts a message using AES encryption a provided encryption key
        /// </summary>
        /// <param name="encryptionKey">The encryption key</param>
        /// <param name="message">The message to be decrypted as an encrypted byte array</param>
        /// <returns>The decrypted messaged as a byte array</returns>
        internal async static Task<byte[]> DecryptMessage(byte[] encryptionKey, byte[] message)
        {
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = encryptionKey;

                // Extract IV from the message
                byte[] iv = new byte[aesAlg.BlockSize / 8];
                Buffer.BlockCopy(message, 0, iv, 0, iv.Length);

                // Remove IV from the message
                byte[] encryptedMessage = new byte[message.Length - iv.Length];
                Buffer.BlockCopy(message, iv.Length, encryptedMessage, 0, encryptedMessage.Length);

                aesAlg.IV = iv;

                try
                {
                    using (MemoryStream msDecrypt = new MemoryStream(encryptedMessage))
                    {
                        using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV), CryptoStreamMode.Read))
                        {
                            using (MemoryStream decryptedData = new MemoryStream())
                            {
                                    int bytesRead;
                                    byte[] buffer = new byte[1024];

                                    while ((bytesRead = csDecrypt.Read(buffer, 0, buffer.Length)) > 0)
                                    {
                                        await decryptedData.WriteAsync(buffer, 0, bytesRead);
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