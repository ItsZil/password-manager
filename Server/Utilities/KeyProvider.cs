using System.Security.Cryptography;

namespace Server.Utilities
{
    // Singleton
    public class KeyProvider
    {
        private byte[]? SharedSecret;

        internal byte[] ComputeSharedSecret(byte[] clientPublicKey)
        {
            using ECDiffieHellman client = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP521);
            client.ImportSubjectPublicKeyInfo(clientPublicKey, out _);

            using ECDiffieHellman server = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP521);
            byte[] serverPublicKey = server.ExportSubjectPublicKeyInfo();

            SharedSecret = server.DeriveKeyMaterial(client.PublicKey);
            return serverPublicKey;
        }

        internal byte[] ComputeSharedSecretTests(ECDiffieHellman client, byte[] serverPublicKey)
        {
            using ECDiffieHellman server = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP521);
            server.ImportSubjectPublicKeyInfo(serverPublicKey, out _);

            SharedSecret = client.DeriveKeyMaterial(server.PublicKey);
            return SharedSecret;
        }

        internal byte[] GetSharedSecret()
        {
            return SharedSecret ?? throw new Exception("Shared secret is null - has handshake completed?");
        }

        // For use in tests
        internal byte[] GenerateClientPublicKey(out ECDiffieHellman client)
        {
            ECDiffieHellman clientECDH = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP521);
            byte[] key = clientECDH.ExportSubjectPublicKeyInfo();

            client = clientECDH;
            return key;
        }
    }
}
