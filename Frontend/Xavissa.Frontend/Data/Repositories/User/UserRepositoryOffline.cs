using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xavissa.Frontend.Data.Entities;
using Xavissa.Frontend.Models;
using Xavissa.Frontend.Models.Auth;

namespace Xavissa.Frontend.Data.Repositories
{
    public class UserRepositoryOffline : IUserRepositoryOffline
    {
        private readonly IDbContextFactory<LocalDbContext> _factory;

        public UserRepositoryOffline(IDbContextFactory<LocalDbContext> factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public async Task<List<OfflineIdentity>> GetAllAsync()
        {
            await using var db = _factory.CreateDbContext();
            return await db.OfflineIdentities.AsNoTracking().ToListAsync();
        }

        public async Task<OfflineIdentity?> GetByUsernameAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return null;
            await using var db = _factory.CreateDbContext();
            var normalizedUsername = NormalizeUsername(username);
            return await db.OfflineIdentities
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Username.Trim().ToLower() == normalizedUsername);
        }

        public async Task SyncFromServerAsync(List<User> usersFromServer)
        {
            if (usersFromServer == null)
                throw new ArgumentNullException(nameof(usersFromServer));

            await using var db = _factory.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();

            try
            {
                db.ChangeTracker.Clear();

                var existingUsers = await db.OfflineIdentities.ToListAsync();
                var byOnlineUserId = existingUsers
                    .Where(user => user.OnlineUserId > 0)
                    .GroupBy(user => user.OnlineUserId)
                    .ToDictionary(group => group.Key, group => group.First());
                var byNormalizedUsername = existingUsers
                    .GroupBy(user => NormalizeUsername(user.Username))
                    .ToDictionary(group => group.Key, group => group.First());

                foreach (var user in usersFromServer)
                {
                    var normalizedUsername = NormalizeUsername(user.Username);
                    var onlineUserId = user.EffectiveOnlineUserId;
                    if (!byOnlineUserId.TryGetValue(onlineUserId, out var offlineUser)
                        && !byNormalizedUsername.TryGetValue(normalizedUsername, out offlineUser))
                    {
                        offlineUser = new OfflineIdentity
                        {
                            OnlineUserId = onlineUserId,
                            Username = user.Username.Trim(),
                            LastOnlineLogin = DateTime.UtcNow,
                        };
                        await db.OfflineIdentities.AddAsync(offlineUser);
                    }

                    offlineUser.OnlineUserId = onlineUserId;
                    offlineUser.Username = user.Username.Trim();
                    offlineUser.Role = string.IsNullOrWhiteSpace(user.ActingRole) ? user.PlatformRole : user.ActingRole;
                    offlineUser.PlatformRoleCode = AppRoles.NormalizeRoleCode(user.PlatformRole);
                    offlineUser.PlatformRole = user.PlatformRole;
                    offlineUser.ActingRole = user.ActingRole;
                    offlineUser.IsActive = user.IsActive;

                    if (string.IsNullOrWhiteSpace(offlineUser.PasswordHash) && !string.IsNullOrWhiteSpace(user.PasswordHash))
                        offlineUser.PasswordHash = user.PasswordHash;

                    byOnlineUserId[onlineUserId] = offlineUser;
                    byNormalizedUsername[normalizedUsername] = offlineUser;
                }

                await db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        private static string NormalizeUsername(string username) => username.Trim().ToLower();
    }
}
