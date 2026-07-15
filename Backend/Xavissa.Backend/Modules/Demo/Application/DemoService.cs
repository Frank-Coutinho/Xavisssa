using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Xavissa.Backend.DTOs;
using Xavissa.Database;
using Xavissa.Database.Models;

namespace Xavissa.Backend.Services;

public class DemoService : IDemoService
{
    private readonly XavissaDbContext _db;

    public DemoService(XavissaDbContext db)
    {
        _db = db;
    }

    public async Task<DemoStartResponse> StartDemoAsync(DemoStartRequest request, string? ipAddress)
    {
        var templates = _db
            .DemoTemplates.IgnoreQueryFilters()
            .Include(x => x.TemplateTenant)
            .Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(request.DemoTemplateCode))
            templates = templates.Where(x => x.Name == request.DemoTemplateCode || x.SeedVersion == request.DemoTemplateCode);

        var template = await templates.FirstOrDefaultAsync();
        if (template == null)
            throw new InvalidOperationException("No active demo template is configured.");

        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(60);
        var demoTenant = new Tenant
        {
            Name = "Loja Demo Xavissa",
            Code = $"DEMO-{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            IsActive = true,
            IsDemo = true,
            IsDemoTemplate = false,
            DemoExpiresAt = expiresAt,
            SourceDemoTemplateId = template.Id,
        };
        _db.Tenants.Add(demoTenant);
        await _db.SaveChangesAsync();

        await CloneSeedDataAsync(template.TemplateTenantId, demoTenant.Id);

        var demoToken = GenerateToken();
        var session = new DemoSession
        {
            DemoTemplateId = template.Id,
            TenantId = demoTenant.Id,
            DemoTokenHash = HashToken(demoToken),
            StartedAt = now,
            ExpiresAt = expiresAt,
            LastActivityAt = now,
            IpAddress = ipAddress,
            DeviceFingerprint = request.DeviceFingerprint,
            AppVersion = request.AppVersion,
            Status = "Active",
            IsActive = true,
            ResetOnClose = true,
        };
        _db.DemoSessions.Add(session);
        _db.DemoSessionEvents.Add(new DemoSessionEvent
        {
            DemoSession = session,
            EventType = "DemoStarted",
            Description = "Demo session started.",
            CreatedAt = now,
        });
        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = demoTenant.Id,
            EntityName = "DemoSession",
            ActionType = "DemoStarted",
            Description = "Demo session started.",
            CreatedAt = now,
        });
        await _db.SaveChangesAsync();

        return new DemoStartResponse
        {
            Success = true,
            DemoSessionId = session.Id,
            TenantId = demoTenant.Id,
            TenantCode = demoTenant.Code,
            TenantName = demoTenant.Name,
            StartedAt = now,
            ExpiresAt = expiresAt,
            ResetOnClose = true,
            DemoModeEnabled = true,
            DemoToken = demoToken,
        };
    }

    public async Task<ValidateDemoSessionResponse> ValidateDemoAsync(ValidateDemoSessionRequest request)
    {
        var session = await _db.DemoSessions
            .IgnoreQueryFilters()
            .Include(x => x.Tenant)
            .FirstOrDefaultAsync(x =>
                x.Id == request.DemoSessionId
                && x.TenantId == request.TenantId);
        if (session == null)
            return new ValidateDemoSessionResponse { Success = false, FailureMessage = "Demo session was not found." };

        var now = DateTime.UtcNow;
        var expired = !session.IsActive
            || !string.Equals(session.Status, "Active", StringComparison.OrdinalIgnoreCase)
            || session.ExpiresAt <= now;
        if (expired)
        {
            session.Status = "Expired";
            session.IsActive = false;
            await _db.SaveChangesAsync();
        }
        else
        {
            session.LastActivityAt = now;
            await _db.SaveChangesAsync();
        }

        return new ValidateDemoSessionResponse
        {
            Success = true,
            IsExpired = expired,
            ExpiresAt = session.ExpiresAt,
            RemainingSeconds = Math.Max(0, (int)(session.ExpiresAt - now).TotalSeconds),
        };
    }

    public async Task<bool> TrackEventAsync(DemoSessionEventRequest request)
    {
        DemoSession? session;
        if (request.DemoSessionId.HasValue)
        {
            session = await _db.DemoSessions.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == request.DemoSessionId.Value);
        }
        else
        {
            var hash = HashToken(request.DemoToken);
            session = await _db.DemoSessions.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.DemoTokenHash == hash);
        }

        if (session == null)
            return false;

        var now = DateTime.UtcNow;
        if (session.ExpiresAt <= now)
        {
            session.Status = "Expired";
            session.IsActive = false;
            request.EventType = string.IsNullOrWhiteSpace(request.EventType) ? "DemoExpired" : request.EventType;
        }

        session.LastActivityAt = now;
        _db.DemoSessionEvents.Add(new DemoSessionEvent
        {
            DemoSessionId = session.Id,
            EventType = request.EventType,
            EntityName = request.EntityName,
            EntityId = request.EntityId,
            Description = request.Description,
            CreatedAt = now,
        });
        await _db.SaveChangesAsync();
        return true;
    }

    private async Task CloneSeedDataAsync(int templateTenantId, int demoTenantId)
    {
        var stores = await _db.Stores.IgnoreQueryFilters().Where(x => x.TenantId == templateTenantId && x.IsActive).ToListAsync();
        foreach (var store in stores)
        {
            _db.Stores.Add(new Store
            {
                TenantId = demoTenantId,
                Name = store.Name,
                Code = $"{store.Code}-D",
                IsActive = true,
            });
        }

        await _db.SaveChangesAsync();

        var categories = await _db.Categories.IgnoreQueryFilters().Where(x => x.TenantId == templateTenantId).ToListAsync();
        foreach (var category in categories)
        {
            _db.Categories.Add(new Category
            {
                TenantId = demoTenantId,
                Name = category.Name,
                ParentId = null,
                IsActive = category.IsActive,
            });
        }

        var products = await _db.Products.IgnoreQueryFilters().Where(x => x.TenantId == templateTenantId).ToListAsync();
        foreach (var product in products)
        {
            _db.Products.Add(new Product
            {
                TenantId = demoTenantId,
                Name = product.Name,
                Description = product.Description,
                Brand = product.Brand,
                CategoryId = null,
                Code = $"{product.Code}-D",
                IsActive = product.IsActive,
            });
        }

        await _db.SaveChangesAsync();
    }

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string HashToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }
}
