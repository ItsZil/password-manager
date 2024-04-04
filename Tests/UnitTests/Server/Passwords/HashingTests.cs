using Geralt;
using Server.Utilities;

namespace Tests.UnitTests.Server.Passwords
{
    public class HashingTests
    {
        private byte[] _correctVaultPasswordHash;
        private byte[] _incorrectVaultPasswordHash;

        private byte[] _correctEncryptionKey;
        private byte[] _incorrectEncryptionKey;

        //private KeyProvider _keyProvider;

        public HashingTests()
        {
            // Ensure that there is a shared secret to use as key for encryption
            KeyProvider keyProvider = new();
            byte[] clientPublicKey = keyProvider.GenerateClientPublicKey();
            keyProvider.ComputeSharedSecret(clientPublicKey);

            // Store the encryption keys for later use in tests
            _correctEncryptionKey = keyProvider.GetSharedSecret();

            // Make sure the incorrect encryption key is different
            _incorrectEncryptionKey = new byte[_correctEncryptionKey.Length];
            _correctEncryptionKey.AsSpan().CopyTo(_incorrectEncryptionKey);
            _incorrectEncryptionKey[0] = (_correctEncryptionKey[0] == 0 ? (byte)1 : (byte)0);

            // Hash master passwords
            ReadOnlySpan<byte> plainVaultPassword = PasswordUtil.ByteArrayFromPlain("Test123");
            _correctVaultPasswordHash = PasswordUtil.HashMasterPassword(plainVaultPassword);

            //Span<byte> salt = stackalloc byte[16];
            //_correctEncryptionKey = PasswordUtil.DeriveEncryptionKeyFromMasterPassword(_correctVaultPasswordHash, ref salt);

            ReadOnlySpan<byte> plainIncorrectVaultPassword = PasswordUtil.ByteArrayFromPlain("Test1234");
            _incorrectVaultPasswordHash = PasswordUtil.HashMasterPassword(plainIncorrectVaultPassword);
            //_incorrectEncryptionKey = PasswordUtil.DeriveEncryptionKeyFromMasterPassword(_incorrectVaultPasswordHash, ref salt);

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

        /* TODO: Remove these tests
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
        */

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
    }
}
