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

        private string GenerateClientPublicKey()
        {
            using ECDiffieHellman client = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP521);
            return Convert.ToBase64String(client.ExportSubjectPublicKeyInfo());
        }

        [Fact]
        public void TestComputeSharedSecretNotNull()
        {
            string clientPublicKey = GenerateClientPublicKey();
            HandshakeRequest request = new HandshakeRequest { ClientPublicKey = clientPublicKey };

            byte[] serverPublicKey = _keyProvider.ComputeSharedSecret(Convert.FromBase64String(request.ClientPublicKey));

            Assert.NotNull(serverPublicKey);
            Assert.NotNull(_keyProvider.GetSharedSecret());
        }
    }
}
