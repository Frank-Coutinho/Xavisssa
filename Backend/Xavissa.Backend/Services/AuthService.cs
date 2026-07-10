using Microsoft.EntityFrameworkCore;
using Xavissa.Backend.DTOs;
using Xavissa.Backend.Security;
using Xavissa.Backend.Utilities;
using Xavissa.Database;
using Xavissa.Database.Models;

namespace Xavissa.Backend.Services
{
    public class AuthService
    {
        private readonly XavissaDbContext _db;
        private readonly IJwtService _jwtService;
        private readonly IRoleService _roleService;

        public AuthService(XavissaDbContext db, IJwtService jwtService, IRoleService roleService)
        {
            _db = db;
            _jwtService = jwtService;
            _roleService = roleService;
        }

        public async Task<User?> Register(
            string username,
            string email,
            string password,
            string? platformRole = null,
            string? assignedRole = null,
            int? tenantId = null,
            int? storeId = null,
            int? createdBy = null,
            string? creatorPlatformRole = null,
            string? creatorActingRole = null,
            int? creatorSelectedTenantId = null,
            int? creatorSelectedStoreId = null,
            IReadOnlyCollection<int>? creatorAllowedTenantIds = null,
            IReadOnlyCollection<int>? creatorAllowedStoreIds = null
        )
        {
            if (
                string.IsNullOrWhiteSpace(username)
                || string.IsNullOrWhiteSpace(email)
                || string.IsNullOrWhiteSpace(password)
            )
                return null;

            var normalizedUsername = username.Trim();
            var normalizedEmail = email.Trim();
            if (
                await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.Username == normalizedUsername)
            )
                return null;

            if (
                !TryResolveNewUserScope(
                    platformRole,
                    assignedRole,
                    tenantId,
                    storeId,
                    creatorPlatformRole,
                    creatorActingRole,
                    creatorSelectedTenantId,
                    creatorSelectedStoreId,
                    creatorAllowedTenantIds,
                    creatorAllowedStoreIds,
                    out var resolvedPlatformRole,
                    out var resolvedTenantId,
                    out var resolvedTenantRole,
                    out var resolvedStoreId,
                    out var resolvedStoreRole
                )
            )
            {
                return null;
            }

            if (resolvedTenantId.HasValue)
            {
                var tenantExists = await _db
                    .Tenants.IgnoreQueryFilters()
                    .AnyAsync(x => x.Id == resolvedTenantId.Value);
                if (!tenantExists)
                    return null;
            }

            if (resolvedStoreId.HasValue)
            {
                var store = await _db
                    .Stores.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x => x.Id == resolvedStoreId.Value);
                if (store == null)
                    return null;

                if (resolvedTenantId.HasValue && store.TenantId != resolvedTenantId.Value)
                    return null;

                resolvedTenantId ??= store.TenantId;
            }

            var prefix = resolvedPlatformRole.IsPlatformAdmin()
                ? "SYS"
                : resolvedPlatformRole.IsSupport() ? "SUP" : "USR";

            int? platformRoleId = null;
            if (resolvedPlatformRole.IsPlatformAdmin() || resolvedPlatformRole.IsSupport())
                platformRoleId = await _roleService.ResolveRoleIdAsync(resolvedPlatformRole, RoleScopes.Platform);

            int? tenantRoleId = null;
            if (resolvedTenantRole.IsTenantAdmin())
                tenantRoleId = await _roleService.ResolveRoleIdAsync(resolvedTenantRole, RoleScopes.Tenant);

            int? storeRoleId = null;
            if (resolvedStoreId.HasValue)
                storeRoleId = await _roleService.ResolveRoleIdAsync(resolvedStoreRole, RoleScopes.Store);

            var user = new User
            {
                Username = normalizedUsername,
                Email = normalizedEmail,
                PasswordHash = PasswordHasher.HashPassword(password),
                PlatformRoleId = platformRoleId,
                Code = IdGenerator.GenerateId(prefix),
                IsActive = true,
            };

            _db.Users.Add(user);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsDuplicateUsernameViolation(ex))
            {
                return null;
            }

            if (resolvedTenantId.HasValue)
            {
                _db.TenantUsers.Add(
                    new TenantUser
                    {
                        TenantId = resolvedTenantId.Value,
                        UserId = user.Id,
                        TenantRoleId = tenantRoleId,
                        IsActive = true,
                        CreatedBy = createdBy,
                    }
                );
            }

            if (resolvedStoreId.HasValue)
            {
                _db.UserStoreRoles.Add(
                    new UserStoreRole
                    {
                        TenantId = resolvedTenantId,
                        StoreId = resolvedStoreId.Value,
                        UserId = user.Id,
                        RoleId = storeRoleId,
                        IsActive = true,
                        CreatedBy = createdBy,
                    }
                );
            }

            if (resolvedTenantId.HasValue || resolvedStoreId.HasValue)
                await _db.SaveChangesAsync();

            return user;
        }

        private static bool IsDuplicateUsernameViolation(DbUpdateException ex)
        {
            if (ex.InnerException is not Npgsql.PostgresException postgresException)
                return false;

            return postgresException.SqlState == "23505"
                && string.Equals(postgresException.ConstraintName, "unique_username", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<LoginResponse?> LoginAsync(string username, string password)
        {
            var user = await FindUserAsync(username);
            if (user == null || !user.IsActive)
                return null;

            if (!await VerifyAndUpgradePasswordAsync(user, password))
                return null;

            user.LastLoginAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var platformRole = ResolvePlatformRoleCode(user);
            if (
                !platformRole.IsPlatformAdmin()
                && !platformRole.IsSupport()
                && !user.TenantUsers.Any(x => x.IsActive)
                && !user.UserStores.Any(x => x.IsActive)
            )
            {
                return null;
            }

            return BuildLoginResponse(user, null, null, issueTokenIfSingleStore: !ResolvePlatformRoleCode(user).IsPlatformAdmin());
        }

        public async Task<LoginResponse?> SelectStoreAsync(string username, string password, int storeId)
        {
            var user = await FindUserAsync(username);
            if (user == null || !user.IsActive)
                return null;

            if (!await VerifyAndUpgradePasswordAsync(user, password))
                return null;

            user.LastLoginAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var assignment = user.UserStores.FirstOrDefault(us => us.StoreId == storeId && us.IsActive);
            if (assignment == null)
            {
                var store = await _db.Stores.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == storeId);
                if (store == null)
                    return null;

                var tenantRole = user.TenantUsers.FirstOrDefault(x => x.IsActive && x.TenantId == store.TenantId);
                if (!ResolvePlatformRoleCode(user).IsPlatformAdmin() && !ResolveTenantRoleCode(tenantRole ?? new TenantUser()).IsTenantAdmin())
                    return null;

                return BuildLoginResponse(user, store.TenantId, storeId, true, ResolveTenantRoleCode(tenantRole ?? new TenantUser()));
            }

            return BuildLoginResponse(user, assignment.TenantId, assignment.StoreId, true, ResolveStoreRoleCode(assignment));
        }

        public async Task<bool> UpdateUserAsync(
            int userId,
            UpdateUserRequest request,
            int? currentUserId,
            string? currentPlatformRole,
            string? currentActingRole,
            int? selectedTenantId,
            int? selectedStoreId,
            IReadOnlyCollection<int>? allowedTenantIds,
            IReadOnlyCollection<int>? allowedStoreIds)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null)
                return false;

            if (!await CanManageTargetUserAsync(
                    userId,
                    currentUserId,
                    currentPlatformRole,
                    currentActingRole,
                    selectedTenantId,
                    selectedStoreId,
                    allowedTenantIds,
                    allowedStoreIds))
            {
                return false;
            }

            user.Username = string.IsNullOrWhiteSpace(request.Username) ? user.Username : request.Username.Trim();
            user.Email = string.IsNullOrWhiteSpace(request.Email) ? user.Email : request.Email.Trim();
            if (
                !string.IsNullOrWhiteSpace(request.Username)
                && await _db.Users.IgnoreQueryFilters().AnyAsync(u =>
                    u.Id != userId && u.Username == user.Username
                )
            )
                return false;
            if (currentPlatformRole.IsPlatformAdmin())
            {
                var requestedPlatformRole = request.PlatformRoleCode ?? request.PlatformRole;
                if (request.PlatformRoleId.HasValue)
                {
                    if (!await _roleService.ValidateRoleScopeAsync(request.PlatformRoleId.Value, RoleScopes.Platform))
                        return false;
                    user.PlatformRoleId = request.PlatformRoleId.Value;
                }
                else if (!string.IsNullOrWhiteSpace(requestedPlatformRole))
                {
                    user.PlatformRoleId = await _roleService.ResolveRoleIdAsync(requestedPlatformRole, RoleScopes.Platform);
                }
            }
            if (request.IsActive.HasValue)
                user.IsActive = request.IsActive.Value;

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteUserAsync(
            int userId,
            int? currentUserId,
            string? currentPlatformRole,
            string? currentActingRole,
            int? selectedTenantId,
            int? selectedStoreId,
            IReadOnlyCollection<int>? allowedTenantIds,
            IReadOnlyCollection<int>? allowedStoreIds)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null)
                return false;

            if (!await CanManageTargetUserAsync(
                    userId,
                    currentUserId,
                    currentPlatformRole,
                    currentActingRole,
                    selectedTenantId,
                    selectedStoreId,
                    allowedTenantIds,
                    allowedStoreIds))
            {
                return false;
            }

            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
            return true;
        }

        public Task<List<User>> GetAllUsersAsync()
        {
            return _db.Users.OrderBy(u => u.Username).ToListAsync();
        }

        public async Task<List<UserReadDto>> GetUsersForManagementAsync(
            int? currentUserId,
            string? currentPlatformRole,
            string? currentActingRole,
            int? selectedTenantId,
            int? selectedStoreId,
            IReadOnlyCollection<int>? allowedTenantIds,
            IReadOnlyCollection<int>? allowedStoreIds
        )
        {
            var tenantIds = allowedTenantIds?.ToHashSet() ?? new HashSet<int>();
            var storeIds = allowedStoreIds?.ToHashSet() ?? new HashSet<int>();

            var query = _db
                .Users.Include(u => u.PlatformRoleNavigation)
                .Include(u => u.TenantUsers)
                .ThenInclude(tu => tu.TenantRole)
                .Include(u => u.TenantUsers)
                .Include(u => u.UserStores)
                .ThenInclude(us => us.RoleNavigation)
                .Include(u => u.UserStores)
                .ThenInclude(us => us.Store)
                .AsQueryable();

            if (currentUserId.HasValue)
                query = query.Where(u => u.Id != currentUserId.Value);

            if (currentPlatformRole.IsPlatformAdmin())
            {
                return await MapUsersAsync(query, selectedTenantId, selectedStoreId, tenantIds, storeIds);
            }

            if (currentActingRole.IsTenantAdmin())
            {
                query = query.Where(u =>
                    u.TenantUsers.Any(tu => tenantIds.Contains(tu.TenantId))
                    || u.UserStores.Any(us => us.TenantId.HasValue && tenantIds.Contains(us.TenantId.Value))
                );

                query = query.Where(u => !u.TenantUsers.Any(tu =>
                    tu.IsActive
                    && tenantIds.Contains(tu.TenantId)
                    && tu.TenantRole != null
                    && tu.TenantRole.Code == AccessRoles.TenantAdmin));

                return await MapUsersAsync(query, selectedTenantId, selectedStoreId, tenantIds, storeIds);
            }

            if (currentActingRole.IsStoreManager())
            {
                query = query.Where(u => u.UserStores.Any(us => us.IsActive && storeIds.Contains(us.StoreId)));
                return await MapUsersAsync(query, selectedTenantId, selectedStoreId, tenantIds, storeIds);
            }

            return new List<UserReadDto>();
        }

        public ValueTask<User?> GetUserByIdAsync(int userId)
        {
            return _db.Users.FindAsync(userId);
        }

        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            return await FindUserAsync(username);
        }

        private async Task<User?> FindUserAsync(string username)
        {
            return await _db
                .Users.Include(u => u.PlatformRoleNavigation)
                .Include(u => u.TenantUsers)
                .ThenInclude(tu => tu.Tenant)
                .Include(u => u.TenantUsers)
                .ThenInclude(tu => tu.TenantRole)
                .Include(u => u.UserStores)
                .ThenInclude(us => us.Store)
                .Include(u => u.UserStores)
                .ThenInclude(us => us.RoleNavigation)
                .FirstOrDefaultAsync(u => u.Username == username);
        }

        private LoginResponse BuildLoginResponse(
            User user,
            int? selectedTenantId,
            int? selectedStoreId,
            bool forceToken = false,
            string? actingRoleOverride = null,
            bool issueTokenIfSingleStore = true
        )
        {
            var activeTenants = user.TenantUsers
                .Where(tu => tu.IsActive)
                .GroupBy(tu => new { tu.TenantId, Name = tu.Tenant != null ? tu.Tenant.Name : $"Tenant {tu.TenantId}" })
                .Select(g => new LoginTenantDto
                {
                    Id = g.Key.TenantId,
                    Name = g.Key.Name,
                    TenantRoleId = g.Select(x => x.TenantRoleId).FirstOrDefault(),
                    TenantRoleCode = g.Select(ResolveTenantRoleCode).FirstOrDefault() ?? AccessRoles.User,
                })
                .Select(x =>
                {
                    x.Role = x.TenantRoleCode;
                    return x;
                })
                .OrderBy(x => x.Name)
                .ToList();

            var activeStores = user.UserStores
                .Where(us => us.IsActive)
                .Select(us => new LoginStoreDto
                {
                    Id = us.StoreId,
                    TenantId = us.TenantId ?? us.Store?.TenantId ?? 0,
                    Name = us.Store?.Name ?? $"Store {us.StoreId}",
                    StoreRoleId = us.RoleId,
                    StoreRoleCode = ResolveStoreRoleCode(us),
                })
                .Select(x =>
                {
                    x.Role = x.StoreRoleCode;
                    return x;
                })
                .OrderBy(x => x.Name)
                .ToList();

            var selectedStore = selectedStoreId.HasValue ? activeStores.FirstOrDefault(x => x.Id == selectedStoreId.Value) : null;
            var preferredStore = selectedStore ?? activeStores
                .OrderByDescending(x => GetRolePriority(x.Role))
                .ThenBy(x => x.Name)
                .FirstOrDefault();
            var resolvedStoreId = selectedStoreId ?? preferredStore?.Id;
            var tenantId = selectedTenantId ?? selectedStore?.TenantId ?? preferredStore?.TenantId ?? activeTenants.FirstOrDefault()?.Id;
            var selectedTenantRole = tenantId.HasValue
                ? activeTenants.FirstOrDefault(x => x.Id == tenantId.Value)?.Role
                : null;
            var actingRole = actingRoleOverride
                ?? selectedStore?.Role
                ?? (selectedTenantRole.IsTenantAdmin() ? selectedTenantRole : null)
                ?? preferredStore?.Role
                ?? selectedTenantRole;

            var response = new LoginResponse
            {
                UserId = user.Id,
                Username = user.Username,
                PlatformRole = ResolvePlatformRoleCode(user),
                PlatformRoleId = user.PlatformRoleId,
                PlatformRoleCode = ResolvePlatformRoleCode(user),
                ActingRole = actingRole,
                SelectedTenantId = tenantId,
                SelectedStoreId = resolvedStoreId,
                AllowedTenants = activeTenants,
                AllowedStores = activeStores,
            };

            var shouldIssueToken = forceToken
                || ResolvePlatformRoleCode(user).IsPlatformAdmin()
                || ResolvePlatformRoleCode(user).IsSupport()
                || actingRole.IsTenantAdmin()
                || actingRole.IsStoreManager()
                || (issueTokenIfSingleStore && activeStores.Count == 1);

            if (shouldIssueToken)
            {
                response.SelectedStoreId ??= activeStores.Count == 1 ? activeStores[0].Id : null;
                response.SelectedTenantId ??= response.SelectedStoreId.HasValue
                    ? activeStores.FirstOrDefault(x => x.Id == response.SelectedStoreId.Value)?.TenantId
                    : activeTenants.FirstOrDefault()?.Id;

                response.Token = _jwtService.GenerateToken(
                    user.Id,
                    response.PlatformRoleCode ?? response.PlatformRole,
                    response.ActingRole,
                    activeTenants.Select(t => t.Id),
                    activeStores.Select(s => s.Id),
                    response.SelectedTenantId,
                    response.SelectedStoreId
                );
            }

            return response;
        }

        private async Task<List<UserReadDto>> MapUsersAsync(
            IQueryable<User> query,
            int? selectedTenantId,
            int? selectedStoreId,
            HashSet<int> allowedTenantIds,
            HashSet<int> allowedStoreIds
        )
        {
            var users = await query.OrderBy(u => u.Username).ToListAsync();
            return users
                .Select(u =>
                {
                    var selectedStoreRole = selectedStoreId.HasValue
                        ? u.UserStores.FirstOrDefault(us => us.IsActive && us.StoreId == selectedStoreId.Value) is { } selectedStoreAssignment
                            ? ResolveStoreRoleCode(selectedStoreAssignment)
                            : null
                        : null;
                    var accessibleStoreRole = u.UserStores
                        .Where(us => us.IsActive && (allowedStoreIds.Count == 0 || allowedStoreIds.Contains(us.StoreId)))
                        .Select(ResolveStoreRoleCode)
                        .FirstOrDefault();
                    var selectedTenantRole = selectedTenantId.HasValue
                        ? u.TenantUsers.FirstOrDefault(tu => tu.IsActive && tu.TenantId == selectedTenantId.Value) is { } selectedTenantUser
                            ? ResolveTenantRoleCode(selectedTenantUser)
                            : null
                        : null;
                    var accessibleTenantRole = u.TenantUsers
                        .Where(tu => tu.IsActive && (allowedTenantIds.Count == 0 || allowedTenantIds.Contains(tu.TenantId)))
                        .Select(ResolveTenantRoleCode)
                        .FirstOrDefault();

                    return new UserReadDto
                    {
                        Id = u.Id,
                        Username = u.Username,
                        Email = u.Email,
                        IsActive = u.IsActive,
                        PlatformRole = ResolvePlatformRoleCode(u),
                        ActingRole = selectedStoreRole ?? accessibleStoreRole ?? selectedTenantRole ?? accessibleTenantRole,
                        ClaimTypesRole = ResolvePlatformRoleCode(u),
                        AssignedStores = u.UserStores
                            .Where(us => us.IsActive && (allowedStoreIds.Count == 0 || allowedStoreIds.Contains(us.StoreId)))
                            .Select(us => us.Store != null ? us.Store.Name : $"Store {us.StoreId}")
                            .Distinct()
                            .OrderBy(name => name)
                            .ToList(),
                    };
                })
                .ToList();
        }

        private async Task<bool> CanManageTargetUserAsync(
            int userId,
            int? currentUserId,
            string? currentPlatformRole,
            string? currentActingRole,
            int? selectedTenantId,
            int? selectedStoreId,
            IReadOnlyCollection<int>? allowedTenantIds,
            IReadOnlyCollection<int>? allowedStoreIds)
        {
            var manageableUsers = await GetUsersForManagementAsync(
                currentUserId,
                currentPlatformRole,
                currentActingRole,
                selectedTenantId,
                selectedStoreId,
                allowedTenantIds,
                allowedStoreIds);

            return manageableUsers.Any(u => u.Id == userId);
        }

        private bool TryResolveNewUserScope(
            string? requestedPlatformRole,
            string? requestedAssignedRole,
            int? requestedTenantId,
            int? requestedStoreId,
            string? creatorPlatformRole,
            string? creatorActingRole,
            int? creatorSelectedTenantId,
            int? creatorSelectedStoreId,
            IReadOnlyCollection<int>? creatorAllowedTenantIds,
            IReadOnlyCollection<int>? creatorAllowedStoreIds,
            out string resolvedPlatformRole,
            out int? resolvedTenantId,
            out string resolvedTenantRole,
            out int? resolvedStoreId,
            out string resolvedStoreRole
        )
        {
            resolvedPlatformRole = AccessRoles.User;
            resolvedTenantId = null;
            resolvedTenantRole = AccessRoles.User;
            resolvedStoreId = null;
            resolvedStoreRole = AccessRoles.Clerk;

            var normalizedPlatformRole = string.IsNullOrWhiteSpace(requestedPlatformRole)
                ? null
                : requestedPlatformRole.Trim();
            var normalizedAssignedRole = NormalizeAssignmentRole(requestedAssignedRole);
            var allowedTenantIds = creatorAllowedTenantIds?.ToHashSet() ?? new HashSet<int>();
            var allowedStoreIds = creatorAllowedStoreIds?.ToHashSet() ?? new HashSet<int>();

            if (creatorPlatformRole.IsPlatformAdmin())
            {
                if (normalizedPlatformRole.IsSupport())
                {
                    resolvedPlatformRole = AccessRoles.Support;
                    return true;
                }

                if (!normalizedAssignedRole.IsTenantAdmin() || !requestedTenantId.HasValue)
                    return false;

                resolvedTenantId = requestedTenantId.Value;
                resolvedTenantRole = AccessRoles.TenantAdmin;

                return true;
            }

            if (creatorPlatformRole.IsSupport() || creatorActingRole.IsTenantAdmin())
            {
                if (!normalizedAssignedRole.IsStoreManager() && !normalizedAssignedRole.IsClerkLike())
                    return false;

                resolvedTenantId = requestedTenantId ?? creatorSelectedTenantId;
                if (!resolvedTenantId.HasValue || !allowedTenantIds.Contains(resolvedTenantId.Value))
                    return false;

                resolvedTenantRole = AccessRoles.User;
                resolvedStoreRole = normalizedAssignedRole.IsStoreManager()
                    ? AccessRoles.StoreManager
                    : AccessRoles.Clerk;

                if (requestedStoreId.HasValue)
                {
                    resolvedStoreId = requestedStoreId.Value;
                }

                return true;
            }

            if (creatorActingRole.IsStoreManager())
            {
                if (!normalizedAssignedRole.IsClerkLike())
                    return false;

                resolvedTenantId = requestedTenantId ?? creatorSelectedTenantId;
                resolvedStoreId = requestedStoreId ?? creatorSelectedStoreId;
                if (!resolvedTenantId.HasValue || !resolvedStoreId.HasValue)
                    return false;
                if (!allowedTenantIds.Contains(resolvedTenantId.Value) || !allowedStoreIds.Contains(resolvedStoreId.Value))
                    return false;

                resolvedTenantRole = AccessRoles.User;
                resolvedStoreRole = AccessRoles.Clerk;
                return true;
            }

            return false;
        }

        private static string NormalizeAssignmentRole(string? role)
        {
            if (string.IsNullOrWhiteSpace(role))
                return string.Empty;
            if (role.IsTenantAdmin())
                return AccessRoles.TenantAdmin;
            if (role.IsStoreManager())
                return AccessRoles.StoreManager;
            if (role.IsClerkLike())
                return AccessRoles.Clerk;
            return role.Trim();
        }

        private static int GetRolePriority(string? role)
        {
            if (role.IsTenantAdmin())
                return 3;
            if (role.IsStoreManager())
                return 2;
            if (role.IsClerkLike())
                return 1;
            return 0;
        }

        private static string ResolvePlatformRoleCode(User user)
        {
            return user.PlatformRoleNavigation?.Code
                ?? user.PlatformRole.NormalizeRoleCode()
                ?? AccessRoles.User;
        }

        private static string ResolveTenantRoleCode(TenantUser tenantUser)
        {
            return tenantUser.TenantRole?.Code
                ?? AccessRoles.User;
        }

        private static string ResolveStoreRoleCode(UserStoreRole userStoreRole)
        {
            return userStoreRole.RoleNavigation?.Code
                ?? AccessRoles.Clerk;
        }

        private async Task<bool> VerifyAndUpgradePasswordAsync(User user, string password)
        {
            if (!PasswordHasher.VerifyPassword(password, user.PasswordHash, out var needsRehash))
                return false;

            if (needsRehash)
            {
                user.PasswordHash = PasswordHasher.HashPassword(password);
                await _db.SaveChangesAsync();
            }

            return true;
        }
    }
}
