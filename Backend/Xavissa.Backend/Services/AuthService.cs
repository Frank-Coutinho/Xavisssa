using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Xavissa.Database;
using Xavissa.Database.Models;
using Xavissa.Backend.DTOs;
using Xavissa.Backend.Utilities;


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
        public async Task<User> Register(string username, string email, string password, UserRole role)
{
    // Check if user exists...
    
    string prefix = role switch
    {
        UserRole.Admin => "ADM",
        UserRole.Clerk => "CLK",
        UserRole.Superuser => "SUPER",
        _ => "USR"
    };

    var user = new User
    {
        Username = username,
        Email = email,
        PasswordHash = HashPassword(password),
        Role = role,
        Code = IdGenerator.GenerateId(prefix)
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

        public async Task<bool> UpdateUserAsync(int userId, UpdateUserRequest request, string currentUserRole)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null) return false;

            // Admins cannot update Superusers
            if (currentUserRole == "Admin" && user.Role == UserRole.Superuser)
                return false;

            // Update fields
            user.Username = request.Username ?? user.Username;
            user.Email = request.Email ?? user.Email;

            if (request.Role.HasValue)
            {
                // Only Superuser can change roles
                if (currentUserRole == "Superuser")
                    user.Role = request.Role.Value;
            }

            await _db.SaveChangesAsync();
            return true;
        }

        // Delete a user
        public async Task<bool> DeleteUserAsync(int userId, string currentUserRole)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null) return false;

            // Admins cannot delete Superusers
            if (currentUserRole == "Admin" && user.Role == UserRole.Superuser)
                return false;

            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
            return true;
        }

         // Get all users (Superuser only)
        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _db.Users.ToListAsync();
        }

        // Get a specific user by ID (Admin or Superuser)
        public async Task<User?> GetUserByIdAsync(int userId)
        {
            return await _db.Users.FindAsync(userId);
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
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
                new Claim(ClaimTypes.Role, user.Role.ToString())
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
