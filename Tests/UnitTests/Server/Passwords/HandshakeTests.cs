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
            HandshakeRequest request = new HandshakeRequest { ClientPublicKey = clientPublicKey };

            byte[] serverPublicKey = _keyProvider.ComputeSharedSecret(request.ClientPublicKey);

            Assert.NotNull(serverPublicKey);
            Assert.NotNull(_keyProvider.GetSharedSecret());
        }
    }
}
