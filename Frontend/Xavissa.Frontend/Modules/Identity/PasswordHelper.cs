using System;
using System.Security.Cryptography;
using System.Text;

namespace Xavissa.Frontend.Helpers
{
    public static class PasswordHelper
    {
        private const int SaltSize = 16;
        private const int KeySize = 32;
        private const int Iterations = 10000;

        public static string HashPassword(string password)
        {
            using var rng = RandomNumberGenerator.Create();
            var salt = new byte[SaltSize];
            rng.GetBytes(salt);

            using var pbkdf2 = new Rfc2898DeriveBytes(
                password,
                salt,
                Iterations,
                HashAlgorithmName.SHA256
            );

            var key = pbkdf2.GetBytes(KeySize);
            var hashBytes = new byte[SaltSize + KeySize];
            Array.Copy(salt, 0, hashBytes, 0, SaltSize);
            Array.Copy(key, 0, hashBytes, SaltSize, KeySize);
            return Convert.ToBase64String(hashBytes);
        }

        public static bool VerifyPassword(string password, string? hash)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash))
                return false;

            if (TryVerifyPbkdf2(password, hash))
                return true;

            return VerifyLegacySha256(password, hash);
        }

        private static bool TryVerifyPbkdf2(string password, string hash)
        {
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
            Array.Copy(hashBytes, 0, salt, 0, SaltSize);

            using var pbkdf2 = new Rfc2898DeriveBytes(
                password,
                salt,
                Iterations,
                HashAlgorithmName.SHA256
            );

            var key = pbkdf2.GetBytes(KeySize);
            return CryptographicOperations.FixedTimeEquals(
                key,
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
