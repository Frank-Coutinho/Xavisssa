using System.Linq;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Mappers
{
    public static class UserMapper
    {
        public static User FromReadDto(UserReadDto dto)
        {
            return new User
            {
                Id = dto.Id,
                OnlineUserId = dto.Id,
                Username = dto.Username,
                email = dto.Email,
                IsActive = dto.IsActive,
                PlatformRole = dto.PlatformRole,
                ActingRole = dto.ActingRole,
                claimTypesRole = dto.ClaimTypesRole,
                AssignedStores = dto.AssignedStores.ToList(),
                Synced = true,
                allClaims = dto.Claims.Select(c => new Claim { Id = c.Id, type = c.Type, value = c.Value }).ToList(),
            };
        }
    }
}
