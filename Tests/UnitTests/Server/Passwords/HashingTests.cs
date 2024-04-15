using Geralt;
using Server.Utilities;

namespace Tests.UnitTests.Server.Passwords
{
    public class HashingTests
    {
        private byte[] _correctVaultPasswordHash;
        private byte[] _incorrectVaultPasswordHash;

        private byte[] _correctSharedEncryptionKey;
        private byte[] _incorrectSharedEncryptionKey;

        private byte[] _correctVaultEncryptionKey;

        public HashingTests()
        {
            // Ensure that there is a shared secret to use as key for encryption
            KeyProvider keyProvider = new();
            byte[] clientPublicKey = keyProvider.GenerateClientPublicKey(out _);
            keyProvider.ComputeSharedSecret(0, clientPublicKey);

            // Store the shared password encryption keys
            _correctSharedEncryptionKey = keyProvider.GetSharedSecret();

            // Make sure the incorrect encryption key is different
            _incorrectSharedEncryptionKey = new byte[_correctSharedEncryptionKey.Length];
            _correctSharedEncryptionKey.AsSpan().CopyTo(_correctSharedEncryptionKey);
            _incorrectSharedEncryptionKey[0] = (_correctSharedEncryptionKey[0] == 0 ? (byte)1 : (byte)0);

            // Hash master passwords & generate vault encryption keys
            ReadOnlySpan<byte> plainVaultPassword = PasswordUtil.ByteArrayFromPlain("Test123");
            _correctVaultPasswordHash = PasswordUtil.HashMasterPassword(plainVaultPassword);

            Span<byte> salt = stackalloc byte[16];
            _correctVaultEncryptionKey = PasswordUtil.DeriveEncryptionKeyFromMasterPassword(_correctVaultPasswordHash, ref salt);

            ReadOnlySpan<byte> plainIncorrectVaultPassword = PasswordUtil.ByteArrayFromPlain("Test1234");
            _incorrectVaultPasswordHash = PasswordUtil.HashMasterPassword(plainIncorrectVaultPassword);
        }

        [Fact]
        public void TestHashingMasterPasswordHashMatches()
        {
            ReadOnlySpan<byte> plainVaultPassword = PasswordUtil.ByteArrayFromPlain("Test123");

            bool hashMatches = Argon2id.VerifyHash(_correctVaultPasswordHash, plainVaultPassword);

            Assert.True(hashMatches);
        }

        [Fact]
        public void TestHashingMasterPasswordNewHashMatches()
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
        public void TestHashingMasterPasswordVaultEncryptionKeyMatches()
        {
            Span<byte> salt = stackalloc byte[16];
            byte[] encryptionKey = PasswordUtil.DeriveEncryptionKeyFromMasterPassword(_correctVaultPasswordHash, ref salt);

            Assert.Equal(_correctVaultEncryptionKey, encryptionKey);
        }

        [Fact]
        public void TestHashingMasterPasswordVaultEncryptionKeyDoesNotMatch()
        {
            Span<byte> salt = stackalloc byte[16];
            byte[] encryptionKey = PasswordUtil.DeriveEncryptionKeyFromMasterPassword(_incorrectVaultPasswordHash, ref salt);

            Assert.NotEqual(_correctVaultEncryptionKey, encryptionKey);
        }
        
        [Fact]
        public async Task TestEncryptPasswordReturnsEncrypted()
        {
            byte[] plainPassword = PasswordUtil.ByteArrayFromPlain("averylongpasswordwithsomenumbersandcharacters5311#@@!");

            byte[] passwordHash = await PasswordUtil.EncryptPassword(_correctSharedEncryptionKey, plainPassword);

            Assert.NotEqual(plainPassword, passwordHash);
            Assert.NotEqual(PasswordUtil.PlainFromContainer(plainPassword), PasswordUtil.PlainFromContainer(passwordHash));
        }

        [Fact]
        public async Task TestDecryptPasswordCorrectKey()
        {
            byte[] plainPassword = PasswordUtil.ByteArrayFromPlain("averylongpasswordwithsomenumbersandcharacters5311#@@!");
            byte[] passwordHash = await PasswordUtil.EncryptPassword(_correctSharedEncryptionKey, plainPassword);

            byte[] decryptedPasswordPlain = await PasswordUtil.DecryptPassword(_correctSharedEncryptionKey, passwordHash);
            string decryptedPassword = PasswordUtil.PlainFromContainer(decryptedPasswordPlain);

            Assert.Equal(PasswordUtil.PlainFromContainer(plainPassword), decryptedPassword);
        }

        [Fact]
        public async Task TestDecryptPasswordIncorrectKey()
        {
            byte[] plainPassword = PasswordUtil.ByteArrayFromPlain("averylongpasswordwithsomenumbersandcharacters5311#@@!");
            byte[] passwordHash = await PasswordUtil.EncryptPassword(_correctSharedEncryptionKey, plainPassword);

            byte[] decryptedPasswordPlain = await PasswordUtil.DecryptPassword(_incorrectSharedEncryptionKey, passwordHash);
            string decryptedPassword = PasswordUtil.PlainFromContainer(decryptedPasswordPlain);

            Assert.NotEqual(PasswordUtil.PlainFromContainer(plainPassword), decryptedPassword);
        }
    }
}
