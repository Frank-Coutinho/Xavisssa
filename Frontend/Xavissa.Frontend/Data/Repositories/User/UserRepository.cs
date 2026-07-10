using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xavissa.Frontend.Models;
using Xavissa.Frontend.Services;

namespace Xavissa.Frontend.Data.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly IServiceProvider _provider;
        private readonly IUserRepositoryOffline _offline;
        private readonly IConnectivityService _net;
        private readonly IAuthService _auth;

        public UserRepository(
            IServiceProvider provider,
            IUserRepositoryOffline offline,
            IConnectivityService net,
            IAuthService auth)
        {
            _provider = provider;
            _offline = offline;
            _net = net;
            _auth = auth;
        }

        public async Task<List<User>> GetAllAsync()
        {
            if (_net.IsOnline() && _auth.IsOnlineSession)
            {
                try
                {
                    var online = _provider.GetRequiredService<IUserRepositoryOnline>();
                    var usersFromServer = await online.FetchAllFromServerAsync();
                    await _offline.SyncFromServerAsync(usersFromServer);
                    return usersFromServer;
                }
                catch (HttpRequestException ex) when (IsAuthDenied(ex))
                {
                    Console.WriteLine("Users sync denied (403/401). Falling back to local users.");
                }
            }

            var offlineUsers = await _offline.GetAllAsync();
            return offlineUsers.Select(MapOfflineUser).ToList();
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            var offline = await _offline.GetByUsernameAsync(username);
            return offline == null ? null : MapOfflineUser(offline);
        }

        public async Task CreateAsync(CreateUserRequest req)
        {
            if (!_net.IsOnline() || !_auth.IsOnlineSession)
                throw new Exception("Internet required to create user.");

            var online = _provider.GetRequiredService<IUserRepositoryOnline>();
            await online.CreateAsync(req);
            await SyncFromServerAsync();
        }

        public async Task UpdateStatusAsync(int id, bool isActive)
        {
            if (!_net.IsOnline() || !_auth.IsOnlineSession)
                throw new Exception("Internet required.");

            var online = _provider.GetRequiredService<IUserRepositoryOnline>();
            await online.UpdateStatusAsync(id, isActive);
            await SyncFromServerAsync();
        }

        public async Task DeleteAsync(int id)
        {
            if (!_net.IsOnline() || !_auth.IsOnlineSession)
                throw new Exception("Internet required.");

            var online = _provider.GetRequiredService<IUserRepositoryOnline>();
            await online.DeleteAsync(id);
            await SyncFromServerAsync();
        }

        public async Task SyncFromServerAsync()
        {
            if (!_net.IsOnline() || !_auth.IsOnlineSession)
                return;

            var online = _provider.GetRequiredService<IUserRepositoryOnline>();

            try
            {
                var users = await online.FetchAllFromServerAsync();
                await _offline.SyncFromServerAsync(users);
            }
            catch (HttpRequestException ex) when (IsAuthDenied(ex))
            {
                Console.WriteLine("Users sync denied (403/401). Skipping users sync.");
            }
        }

        private static User MapOfflineUser(Data.Entities.OfflineIdentity offline)
        {
            var resolvedOnlineUserId = offline.OnlineUserId > 0 ? offline.OnlineUserId : offline.Id;
            return new User
            {
                Id = resolvedOnlineUserId,
                OnlineUserId = resolvedOnlineUserId,
                Username = offline.Username,
                PasswordHash = offline.PasswordHash,
                PlatformRole = offline.PlatformRole,
                ActingRole = string.IsNullOrWhiteSpace(offline.ActingRole) ? offline.Role : offline.ActingRole,
                IsActive = offline.IsActive,
                Synced = true,
            };
        }

        private static bool IsAuthDenied(HttpRequestException ex)
        {
            return ex.StatusCode == HttpStatusCode.Forbidden || ex.StatusCode == HttpStatusCode.Unauthorized;
        }
    }
}
