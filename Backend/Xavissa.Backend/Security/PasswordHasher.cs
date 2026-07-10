using System;
using System.Security.Cryptography;
using System.Text;

namespace Xavissa.Backend.Security
{
    public static class PasswordHasher
    {
        private const int SaltSize = 16;
        private const int KeySize = 32;
        private const int Iterations = 10000;

        public static string HashPassword(string password)
        {
            ArgumentNullException.ThrowIfNull(password);

            Span<byte> salt = stackalloc byte[SaltSize];
            RandomNumberGenerator.Fill(salt);

            using var pbkdf2 = new Rfc2898DeriveBytes(
                password,
                salt.ToArray(),
                Iterations,
                HashAlgorithmName.SHA256
            );

            var key = pbkdf2.GetBytes(KeySize);
            var hashBytes = new byte[SaltSize + KeySize];
            salt.CopyTo(hashBytes);
            Buffer.BlockCopy(key, 0, hashBytes, SaltSize, KeySize);
            return Convert.ToBase64String(hashBytes);
        }

        public static bool VerifyPassword(string password, string? hash, out bool needsRehash)
        {
            needsRehash = false;

            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash))
                return false;

            if (TryVerifyPbkdf2(password, hash, out needsRehash))
                return true;

            if (VerifyLegacySha256(password, hash))
            {
                needsRehash = true;
                return true;
            }

            return false;
        }

        private static bool TryVerifyPbkdf2(string password, string hash, out bool needsRehash)
        {
            needsRehash = false;

            byte[] hashBytes;
            try
            {
                hashBytes = Convert.FromBase64String(hash);
            }
            catch (FormatException)
            {
                return false;
            }

            if (hashBytes.Length != SaltSize + KeySize)
                return false;

            var salt = new byte[SaltSize];
            Buffer.BlockCopy(hashBytes, 0, salt, 0, SaltSize);

            using var pbkdf2 = new Rfc2898DeriveBytes(
                password,
                salt,
                Iterations,
                HashAlgorithmName.SHA256
            );

            var computedKey = pbkdf2.GetBytes(KeySize);
            return CryptographicOperations.FixedTimeEquals(
                computedKey,
                hashBytes.AsSpan(SaltSize, KeySize).ToArray()
            );
        }

        private static bool VerifyLegacySha256(string password, string hash)
        {
            using var sha256 = SHA256.Create();
            var computed = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            var expected = Convert.ToBase64String(computed);
            return string.Equals(expected, hash, StringComparison.Ordinal);
        }
    }
}
