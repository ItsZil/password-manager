using System.Security.Cryptography;

namespace Server.Utilities
{
    internal static class PasskeyUtil
    {
        internal static bool VerifyPasskeySignature(byte[] publicKey, byte[] data, byte[] signature)
        {
            // Something is very weird here. If we use the IeeeP1363FixedFieldConcatenation signature format, the verification ocassionally fails.
            // The Rfc3279DerSequence format seems to work consistently, but the signature must remain in DER format.
            try
            {
                using ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                ecdsa.ImportSubjectPublicKeyInfo(publicKey, out _);

                return ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
            }
            catch (CryptographicException ex)
            {
                Console.WriteLine($"Error verifying signature: {ex.Message}");
                return false;
            }
        }

        internal static bool IsUserVerificationCompleted(string authenticatorDataBase64)
        {
            try
            {
                // Decode base64 string into byte array
                byte[] authenticatorData = Convert.FromBase64String(authenticatorDataBase64);

                // Get the flags byte
                byte flags = authenticatorData[32];

                // Check if user verification (UV) flag is set (second bit)
                return (flags & (1 << 2)) != 0;
            }
            catch (FormatException ex)
            {
                Console.WriteLine($"Error decoding base64 string: {ex.Message}");
                return false;
            }
            catch (IndexOutOfRangeException ex)
            {
                Console.WriteLine($"Error accessing flags byte: {ex.Message}");
                return false;
            }
        }

        internal static byte[] ConvertFromASN1(byte[] sig)
        {
            const int DER = 48;
            const int LENGTH_MARKER = 2;

            if (sig.Length < 6 || sig[0] != DER || sig[1] != sig.Length - 2 || sig[2] != LENGTH_MARKER || sig[sig[3] + 4] != LENGTH_MARKER)
                throw new ArgumentException("Invalid signature format.", "sig");

            int rLen = sig[3];
            int sLen = sig[rLen + 5];

            byte[] newSig = new byte[rLen + sLen];
            Buffer.BlockCopy(sig, 4, newSig, 0, rLen);
            Buffer.BlockCopy(sig, 6 + rLen, newSig, rLen, sLen);

            return newSig;
        }

        internal static byte[] ConcatenateArrays(byte[] array1, byte[] array2)
        {
            byte[] result = new byte[array1.Length + array2.Length];
            Buffer.BlockCopy(array1, 0, result, 0, array1.Length);
            Buffer.BlockCopy(array2, 0, result, array1.Length, array2.Length);

            return result;
        }
    }
}
