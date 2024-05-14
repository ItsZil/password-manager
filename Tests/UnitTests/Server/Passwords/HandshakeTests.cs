using Server.Utilities;
using UtilitiesLibrary.Models;

namespace Tests.UnitTests
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
            HandshakeRequest request = new HandshakeRequest { SourceId = 0, ClientPublicKeyBase64 = Convert.ToBase64String(clientPublicKey) };

            byte[] serverPublicKey = _keyProvider.ComputeSharedSecret(0, Convert.FromBase64String(request.ClientPublicKeyBase64));

            Assert.NotNull(serverPublicKey);
            Assert.NotNull(_keyProvider.GetSharedSecret());
        }
    }
}
