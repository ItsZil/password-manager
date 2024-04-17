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

        private byte[] _correctEncryptionKey;
        private byte[] _correctEncryptionSalt;

        private KeyProvider _keyProvider;

        public HashingTests()
        {
            // Ensure that there is a shared secret to use as key for encryption
            _keyProvider = new();
            byte[] clientPublicKey = _keyProvider.GenerateClientPublicKey(out _);
            _keyProvider.ComputeSharedSecret(0, clientPublicKey);

            // Store the shared password encryption keys
            _correctSharedEncryptionKey = _keyProvider.GetSharedSecret();

            // Store a vault pragma key
            _keyProvider.SetVaultPragmaKey("Test123");

            // Make sure the incorrect encryption key is different
            _incorrectSharedEncryptionKey = new byte[_correctSharedEncryptionKey.Length];
            _correctSharedEncryptionKey.AsSpan().CopyTo(_correctSharedEncryptionKey);
            _incorrectSharedEncryptionKey[0] = (_correctSharedEncryptionKey[0] == 0 ? (byte)1 : (byte)0);

            // Hash master passwords & generate vault encryption keys
            ReadOnlySpan<byte> plainVaultPassword = PasswordUtil.ByteArrayFromPlain("Test123");
            _correctVaultPasswordHash = PasswordUtil.HashMasterPassword(plainVaultPassword);

            Span<byte> salt = stackalloc byte[16];
            _correctEncryptionKey = PasswordUtil.DeriveEncryptionKeyFromPassword(PasswordUtil.ByteArrayFromPlain("Test1234"), ref salt);
            _correctEncryptionSalt = salt.ToArray();

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
        public void TestDerivingEncryptionKeySamePasswordMatches()
        {
            byte[] encryptionKey = PasswordUtil.DeriveEncryptionKeyFromPassword(PasswordUtil.ByteArrayFromPlain("Test1234"), _correctEncryptionSalt);

            Assert.Equal(_correctEncryptionKey, encryptionKey);
        }

        [Fact]
        public void TestDerivingEncryptionKeySamePasswordDoesNotMatch()
        {
            byte[] encryptionKey = PasswordUtil.DeriveEncryptionKeyFromPassword(PasswordUtil.ByteArrayFromPlain("Test12345"), _correctEncryptionSalt);

            Assert.NotEqual(_correctEncryptionKey, encryptionKey);
        }
        
        [Fact]
        public async Task TestEncryptPasswordReturnsEncryptedAndSalt()
        {
            byte[] plainPassword = PasswordUtil.ByteArrayFromPlain("averylongpasswordwithsomenumbersandcharacters5311#@@!");

            (byte[] passwordHash, byte[] salt) = await PasswordUtil.EncryptPassword(plainPassword, _keyProvider.GetVaultPragmaKeyBytes());

            Assert.NotNull(passwordHash);
            Assert.NotNull(salt);

            Assert.NotEqual(plainPassword, passwordHash);
            Assert.NotEqual(PasswordUtil.PlainFromContainer(plainPassword), PasswordUtil.PlainFromContainer(passwordHash));
        }

        [Fact]
        public async Task TestDecryptPasswordCorrectSalt()
        {
            byte[] plainPassword = PasswordUtil.ByteArrayFromPlain("averylongpasswordwithsomenumbersandcharacters5311#@@!");
            (byte[] passwordHash, byte[] salt) = await PasswordUtil.EncryptPassword(plainPassword, _keyProvider.GetVaultPragmaKeyBytes());

            byte[] decryptedPasswordPlain = await PasswordUtil.DecryptPassword(passwordHash, salt, _keyProvider.GetVaultPragmaKeyBytes());
            string decryptedPassword = PasswordUtil.PlainFromContainer(decryptedPasswordPlain);

            Assert.Equal(PasswordUtil.PlainFromContainer(plainPassword), decryptedPassword);
        }

        [Fact]
        public async Task TestDecryptPasswordIncorrectSalt()
        {
            byte[] plainPassword = PasswordUtil.ByteArrayFromPlain("averylongpasswordwithsomenumbersandcharacters5311#@@!");
            (byte[] passwordHash, byte[] salt) = await PasswordUtil.EncryptPassword(plainPassword, _keyProvider.GetVaultPragmaKeyBytes());

            byte[] randomSalt = new byte[16];
            SecureRandom.Fill(randomSalt);

            byte[] decryptedPasswordPlain = await PasswordUtil.DecryptPassword(passwordHash, randomSalt, _keyProvider.GetVaultPragmaKeyBytes());
            string decryptedPassword = PasswordUtil.PlainFromContainer(decryptedPasswordPlain);

            Assert.NotEqual(PasswordUtil.PlainFromContainer(plainPassword), decryptedPassword);
        }

        [Fact]
        public async Task TestDecryptPasswordIncorrectPragmaKey()
        {
            byte[] plainPassword = PasswordUtil.ByteArrayFromPlain("averylongpasswordwithsomenumbersandcharacters5311#@@!");
            (byte[] passwordHash, byte[] salt) = await PasswordUtil.EncryptPassword(plainPassword, _keyProvider.GetVaultPragmaKeyBytes());

            byte[] randomSalt = new byte[16];
            SecureRandom.Fill(randomSalt);

            byte[] decryptedPasswordPlain = await PasswordUtil.DecryptPassword(passwordHash, randomSalt, randomSalt);
            string decryptedPassword = PasswordUtil.PlainFromContainer(decryptedPasswordPlain);

            Assert.NotEqual(PasswordUtil.PlainFromContainer(plainPassword), decryptedPassword);
        }
    }
}
