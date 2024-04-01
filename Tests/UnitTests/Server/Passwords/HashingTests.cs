using Geralt;
using Server.Utilities;
using System;
using System.Security.Cryptography;
using System.Text;

namespace Tests.UnitTests.Server.Passwords
{
    public class HashingTests
    {
        private byte[] _correctVaultPasswordHash;
        private byte[] _incorrectVaultPasswordHash;

        private byte[] _salt; // TODO: This will probably need to be store in the database instead, because we only need it for existing password decryptions.
        private byte[] _correctEncryptionKey;
        private byte[] _incorrectEncryptionKey;

        public HashingTests()
        {
            ReadOnlySpan<byte> plainVaultPassword = PasswordUtil.ByteArrayFromPlain("Test123");
            _correctVaultPasswordHash = PasswordUtil.HashMasterPassword(plainVaultPassword);

            Span<byte> salt = stackalloc byte[16];
            _correctEncryptionKey = PasswordUtil.DeriveEncryptionKeyFromMasterPassword(_correctVaultPasswordHash, ref salt);

            ReadOnlySpan<byte> plainIncorrectVaultPassword = PasswordUtil.ByteArrayFromPlain("Test1234");
            _incorrectVaultPasswordHash = PasswordUtil.HashMasterPassword(plainIncorrectVaultPassword);
            _incorrectEncryptionKey = PasswordUtil.DeriveEncryptionKeyFromMasterPassword(_incorrectVaultPasswordHash, ref salt);

            _salt = salt.ToArray();
        }

        [Fact]
        public void TestHashingMasterPasswordHashMatches()
        {
            ReadOnlySpan<byte> plainVaultPassword = PasswordUtil.ByteArrayFromPlain("Test123");

            bool hashMatches = Argon2id.VerifyHash(_correctVaultPasswordHash, plainVaultPassword);

            Assert.True(hashMatches);
        }

        [Fact]
        public void TestHashingMasterPasswordDoesNotMatch()
        {
            ReadOnlySpan<byte> plainIncorrectVaultPassword = PasswordUtil.ByteArrayFromPlain("Test1234");

            bool hashMatches = Argon2id.VerifyHash(_correctVaultPasswordHash, plainIncorrectVaultPassword);

            Assert.False(hashMatches);
        }

        [Fact]
        public void TestHashingMasterPasswordEncryptionKeyMatches()
        {
            Span<byte> salt = stackalloc byte[16];
            byte[] encryptionKey = PasswordUtil.DeriveEncryptionKeyFromMasterPassword(_correctVaultPasswordHash, ref salt);

            Assert.Equal(_correctEncryptionKey, encryptionKey);
        }

        [Fact]
        public void TestHashingMasterPasswordEncryptionKeyDoesNotMatch()
        {
            Span<byte> salt = stackalloc byte[16];
            byte[] encryptionKey = PasswordUtil.DeriveEncryptionKeyFromMasterPassword(_incorrectVaultPasswordHash, ref salt);

            Assert.NotEqual(_correctEncryptionKey, encryptionKey);
        }

        [Fact]
        public void TestEncryptPasswordReturnsEncrypted()
        {
            byte[] plainPassword = PasswordUtil.ByteArrayFromPlain("averylongpasswordwithsomenumbersandcharacters5311#@@!");

            byte[] passwordHash = PasswordUtil.EncryptPassword(_correctEncryptionKey, plainPassword);

            Assert.NotEqual(plainPassword, passwordHash);
            Assert.NotEqual(PasswordUtil.PlainFromContainer(plainPassword), PasswordUtil.PlainFromContainer(passwordHash));
        }

        [Fact]
        public void TestDecryptPasswordCorrectMasterHashMatches()
        {
            byte[] plainPassword = PasswordUtil.ByteArrayFromPlain("averylongpasswordwithsomenumbersandcharacters5311#@@!");
            byte[] passwordHash = PasswordUtil.EncryptPassword(_correctEncryptionKey, plainPassword);

            string decryptedPassword = PasswordUtil.DecryptPassword(_correctEncryptionKey, passwordHash);

            Assert.Equal(PasswordUtil.PlainFromContainer(plainPassword), decryptedPassword);
        }

        [Fact]
        public void TestDecryptPasswordIncorrectMasterHashDoesNotMatch()
        {
            byte[] plainPassword = PasswordUtil.ByteArrayFromPlain("averylongpasswordwithsomenumbersandcharacters5311#@@!");
            byte[] passwordHash = PasswordUtil.EncryptPassword(_correctEncryptionKey, plainPassword);

            string decryptedPassword = PasswordUtil.DecryptPassword(_incorrectEncryptionKey, passwordHash);

            Assert.NotEqual(PasswordUtil.PlainFromContainer(plainPassword), decryptedPassword);
        }

        [Fact]
        public void TestFullFlowWithGuaranteedSameSaltPasses()
        {
            byte[] plainPassword = PasswordUtil.ByteArrayFromPlain("averylongpasswordwithsomenumbersandcharacters5311#@@!");

            Span<byte> salt = stackalloc byte[16];

            Span<byte> computedHash = stackalloc byte[128];
            SecureRandom.Fill(salt);

            Argon2id.DeriveKey(computedHash, plainPassword, salt, 3, 67108864);

            Span<byte> encryptionKey = stackalloc byte[32];
            ReadOnlySpan<byte> personalization = Encoding.UTF8.GetBytes("PasswordManagerZ"); // has to be 16 bytes

            BLAKE2b.DeriveKey(encryptionKey, computedHash[..32], personalization, salt);

            byte[] encryptKey = encryptionKey.ToArray();
            byte[] passwordHash = PasswordUtil.EncryptPassword(encryptKey, plainPassword);

            string decryptedPassword = PasswordUtil.DecryptPassword(encryptKey, passwordHash);

            Assert.Equal(PasswordUtil.PlainFromContainer(plainPassword), decryptedPassword);
        }
    }
}
