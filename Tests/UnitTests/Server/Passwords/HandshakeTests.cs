using Server.Utilities;
using System.Security.Cryptography;
using UtilitiesLibrary.Models;

namespace Tests.UnitTests.Server.Passwords
{
    public class HandshakeTests
    {
        private readonly KeyProvider _keyProvider;

        public HandshakeTests()
        {
            _keyProvider = new KeyProvider();
        }

        [Fact]
        public void TestComputeSharedSecretNotNull()
        {
            byte[] clientPublicKey = _keyProvider.GenerateClientPublicKey(out _);
            HandshakeRequest request = new HandshakeRequest { ClientPublicKeyBase64 = Convert.ToBase64String(clientPublicKey) };

            byte[] serverPublicKey = _keyProvider.ComputeSharedSecret(Convert.FromBase64String(request.ClientPublicKeyBase64));

            Assert.NotNull(serverPublicKey);
            Assert.NotNull(_keyProvider.GetSharedSecret());
        }
    }
}
