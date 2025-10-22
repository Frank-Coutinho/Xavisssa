using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Xavissa.Database;
using Xavissa.Database.Models;

namespace Xavissa.Backend.Services
{
    public class AuthService
    {
        private readonly XavissaDbContext _db;
        private readonly IConfiguration _config;

        public AuthService(XavissaDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        // Register user
        public async Task<User> Register(string username, string email, string password)
        {
            var userExists = await _db.Users.AnyAsync(u => u.Username == username || u.Email == email);
            if (userExists) return null;

            var user = new User
            {
                Username = username,
                Email = email,
                PasswordHash = HashPassword(password)
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return user;
        }

        // Login user
        public async Task<string> Login(string username, string password)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return null;

            if (!VerifyPassword(password, user.PasswordHash)) return null;

            return GenerateJwtToken(user);
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashed = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashed);
        }

        private bool VerifyPassword(string password, string hash)
        {
            return HashPassword(password) == hash;
        }

        private string GenerateJwtToken(User user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username)
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(12),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
