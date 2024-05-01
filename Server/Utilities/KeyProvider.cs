using Geralt;
using System.Security.Cryptography;
using System.Text;

namespace Server.Utilities
{
    // Singleton
    public class KeyProvider
    {
        private string _vaultPragmaKey = string.Empty;

        // Key: Source ID (0 - background, 1 - public scripts, 2 - popup), Value: Shared Secret
        private Dictionary<int, byte[]> SharedSecrets = new Dictionary<int, byte[]>();

        internal bool HasVaultPragmaKey()
        {
            return !string.IsNullOrEmpty(_vaultPragmaKey);
        }

        internal void SetVaultPragmaKey(string pragmaKey)
        {
            byte[] hash = new byte[32];
            BLAKE2b.ComputeHash(hash, Encoding.UTF8.GetBytes(pragmaKey));

            _vaultPragmaKey = Convert.ToBase64String(hash);
        }

        internal void SetVaultPragmaKeyHashed(string base64HashedPragmaKey)
        {
            _vaultPragmaKey = base64HashedPragmaKey;
        }

        internal void ClearPragmaKey()
        {
            _vaultPragmaKey = string.Empty;
        }

        internal string GetVaultPragmaKey()
        {
            return _vaultPragmaKey;
        }

        internal byte[] GetVaultPragmaKeyBytes()
        {
            return Encoding.UTF8.GetBytes(_vaultPragmaKey);
        }

        internal byte[] ComputeSharedSecret(int sourceId, byte[] clientPublicKey)
        {
            using ECDiffieHellman client = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            client.ImportSubjectPublicKeyInfo(clientPublicKey, out _);

            using ECDiffieHellman server = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            byte[] serverPublicKey = server.ExportSubjectPublicKeyInfo();

            byte[] sharedSecret = server.DeriveKeyMaterial(client.PublicKey);
            SharedSecrets[sourceId] = sharedSecret;
            return serverPublicKey;
        }

        public string PrintByteArray(byte[] bytes)
        {
            var sb = new StringBuilder("{ ");
            foreach (var b in bytes)
            {
                sb.Append(b + ", ");
            }
            sb.Append("}");
            return sb.ToString();
        }

        internal byte[] ComputeSharedSecretTests(ECDiffieHellman client, byte[] serverPublicKey)
        {
            using ECDiffieHellman server = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            server.ImportSubjectPublicKeyInfo(serverPublicKey, out _);

            SharedSecrets[0] = client.DeriveKeyMaterial(server.PublicKey);
            return SharedSecrets[0];
        }

        internal byte[] GetSharedSecret(int sourceId = 0)
        {
            SharedSecrets.TryGetValue(sourceId, out byte[]? sharedSecret);

            if (sharedSecret == null)
            {
                throw new InvalidOperationException($"Shared secret {sourceId} not found.");
            }

            return sharedSecret;
        }

        internal bool SharedSecretNotNull()
        {
            return SharedSecrets.Count != 0;
        }

        // For use in tests
        internal byte[] GenerateClientPublicKey(out ECDiffieHellman client)
        {
            ECDiffieHellman clientECDH = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            byte[] key = clientECDH.ExportSubjectPublicKeyInfo();

            client = clientECDH;
            return key;
        }
    }
}
