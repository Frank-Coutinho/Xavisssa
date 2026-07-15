using Microsoft.EntityFrameworkCore;
using Xavissa.Backend.DTOs;
using Xavissa.Backend.Security;
using Xavissa.Database;
using Xavissa.Database.Models;

namespace Xavissa.Backend.Services;

public class CashRegisterService
{
    private readonly XavissaDbContext _db;
    private readonly TenantAccessService _tenantAccess;

    public CashRegisterService(XavissaDbContext db, TenantAccessService tenantAccess)
    {
        _db = db;
        _tenantAccess = tenantAccess;
    }

    public async Task<CashRegisterSession> OpenAsync(CashRegisterOpenRequestDto request)
    {
        var userId = RequireUser();
        var storeId = ResolveStore(request.StoreId);
        if (request.OpeningCashAmount < 0)
            throw new ArgumentException("OpeningCashAmount cannot be negative.");

        var store = await RequireStoreAsync(storeId);
        var duplicate = await FindOpenSessionQuery(store.TenantId!.Value, storeId, userId, request.SourceDeviceId)
            .AnyAsync();
        if (duplicate)
            throw new InvalidOperationException("An open cash register session already exists for this store, user, and device.");

        var session = new CashRegisterSession
        {
            TenantId = store.TenantId,
            StoreId = storeId,
            OpenedByUserId = userId,
            SourceDeviceId = request.SourceDeviceId,
            OpeningCashAmount = request.OpeningCashAmount,
            Status = "Open",
            Notes = request.Notes,
            CreatedBy = userId,
            UpdatedBy = userId,
        };

        _db.CashRegisterSessions.Add(session);
        await _db.SaveChangesAsync();
        return session;
    }

    public async Task<CashRegisterSession> CloseAsync(CashRegisterCloseRequestDto request)
    {
        var userId = RequireUser();
        if (!request.CountedCashAmount.HasValue)
            throw new ArgumentException("CountedCashAmount is required.");

        var session = request.SessionId.HasValue
            ? await _db.CashRegisterSessions.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == request.SessionId.Value)
            : await GetCurrentAsync(request.StoreId, request.SourceDeviceId);
        if (session == null)
            throw new InvalidOperationException("No open cash register session was found.");
        EnsureCanUseSession(session, userId, inspectOnly: false);

        var summary = await CalculateSummaryAsync(session);
        session.ExpectedCashAmount = summary.OpeningCashAmount + summary.CashSalesTotal + summary.CashInTotal - summary.CashOutTotal;
        session.CountedCashAmount = request.CountedCashAmount.Value;
        session.DifferenceAmount = request.CountedCashAmount.Value - session.ExpectedCashAmount;
        session.ClosedByUserId = userId;
        session.ClosedAt = DateTime.UtcNow;
        session.Status = "Closed";
        session.Notes = string.IsNullOrWhiteSpace(request.Notes) ? session.Notes : request.Notes;
        session.UpdatedAt = DateTime.UtcNow;
        session.UpdatedBy = userId;
        await _db.SaveChangesAsync();
        return session;
    }

    public async Task<CashRegisterSession?> GetCurrentAsync(int? storeId, string? sourceDeviceId)
    {
        var userId = RequireUser();
        var resolvedStoreId = ResolveStore(storeId);
        var store = await RequireStoreAsync(resolvedStoreId);
        return await FindOpenSessionQuery(store.TenantId!.Value, resolvedStoreId, userId, sourceDeviceId)
            .OrderByDescending(x => x.OpenedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<CashRegisterCashMovement> AddMovementAsync(CashRegisterMovementRequestDto request)
    {
        var userId = RequireUser();
        if (request.Amount <= 0)
            throw new ArgumentException("Movement amount must be greater than zero.");

        var movementType = NormalizeMovementType(request.MovementType);
        if (movementType == null)
            throw new ArgumentException("MovementType must be CashIn or CashOut.");

        var session = request.SessionId.HasValue
            ? await _db.CashRegisterSessions.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == request.SessionId.Value && x.Status == "Open")
            : await GetCurrentAsync(request.StoreId, request.SourceDeviceId);
        if (session == null)
            throw new InvalidOperationException("Cash movements require an open cash register session.");
        EnsureCanUseSession(session, userId, inspectOnly: false);

        if (movementType == "CashOut")
        {
            var summary = await CalculateSummaryAsync(session);
            var expected = summary.OpeningCashAmount + summary.CashSalesTotal + summary.CashInTotal - summary.CashOutTotal;
            if (expected - request.Amount < 0 && !_tenantAccess.CanManageStore(session.StoreId))
                throw new InvalidOperationException("CashOut cannot make expected cash negative without manager authorization.");
        }

        var movement = new CashRegisterCashMovement
        {
            TenantId = session.TenantId,
            StoreId = session.StoreId,
            CashRegisterSessionId = session.Id,
            MovementType = movementType,
            Amount = request.Amount,
            Reason = request.Reason,
            SourceDeviceId = request.SourceDeviceId ?? session.SourceDeviceId,
            CreatedBy = userId,
        };
        _db.CashRegisterCashMovements.Add(movement);
        await _db.SaveChangesAsync();
        return movement;
    }

    public async Task<CashRegisterSummaryDto> GetSummaryAsync(int sessionId)
    {
        var session = await _db.CashRegisterSessions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == sessionId);
        if (session == null)
            throw new KeyNotFoundException("Cash register session was not found.");
        EnsureCanUseSession(session, RequireUser(), inspectOnly: true);
        return await CalculateSummaryAsync(session);
    }

    private async Task<CashRegisterSummaryDto> CalculateSummaryAsync(CashRegisterSession session)
    {
        var view = await _db.CashRegisterSessionSummaries
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CashRegisterSessionId == session.Id);

        var cashSales = view?.CashPaymentsTotal
            ?? await _db.SalePayments.IgnoreQueryFilters()
                .Where(x => x.CashRegisterSessionId == session.Id && x.PaymentMethod.ToLower() == "cash")
                .SumAsync(x => (decimal?)x.Amount) ?? 0;
        var cashIn = view?.CashInTotal
            ?? await _db.CashRegisterCashMovements.IgnoreQueryFilters()
                .Where(x => x.CashRegisterSessionId == session.Id && x.MovementType == "CashIn")
                .SumAsync(x => (decimal?)x.Amount) ?? 0;
        var cashOut = view?.CashOutTotal
            ?? await _db.CashRegisterCashMovements.IgnoreQueryFilters()
                .Where(x => x.CashRegisterSessionId == session.Id && x.MovementType == "CashOut")
                .SumAsync(x => (decimal?)x.Amount) ?? 0;

        return new CashRegisterSummaryDto
        {
            Id = session.Id,
            SyncId = session.SyncId,
            TenantId = session.TenantId,
            StoreId = session.StoreId,
            OpenedByUserId = session.OpenedByUserId,
            ClosedByUserId = session.ClosedByUserId,
            SourceDeviceId = session.SourceDeviceId,
            OpenedAt = session.OpenedAt,
            ClosedAt = session.ClosedAt,
            OpeningCashAmount = session.OpeningCashAmount,
            ExpectedCashAmount = session.ExpectedCashAmount ?? session.OpeningCashAmount + cashSales + cashIn - cashOut,
            CountedCashAmount = session.CountedCashAmount,
            DifferenceAmount = session.DifferenceAmount,
            Status = session.Status,
            Notes = session.Notes,
            CashSalesTotal = cashSales,
            CashInTotal = cashIn,
            CashOutTotal = cashOut,
        };
    }

    private IQueryable<CashRegisterSession> FindOpenSessionQuery(int tenantId, int storeId, int userId, string? sourceDeviceId)
    {
        var query = _db.CashRegisterSessions.IgnoreQueryFilters().Where(x =>
            x.TenantId == tenantId
            && x.StoreId == storeId
            && x.OpenedByUserId == userId
            && x.Status == "Open");
        if (!string.IsNullOrWhiteSpace(sourceDeviceId))
            query = query.Where(x => x.SourceDeviceId == sourceDeviceId);
        return query;
    }

    private async Task<Store> RequireStoreAsync(int storeId)
    {
        if (!_tenantAccess.CanAccessStore(storeId))
            throw new UnauthorizedAccessException("Unauthorized store.");
        var store = await _db.Stores.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == storeId);
        if (store?.TenantId == null)
            throw new InvalidOperationException("Store tenant is missing.");
        return store;
    }

    private void EnsureCanUseSession(CashRegisterSession session, int userId, bool inspectOnly)
    {
        if (!_tenantAccess.CanAccessStore(session.StoreId))
            throw new UnauthorizedAccessException("Unauthorized store.");
        if (inspectOnly && (_tenantAccess.CanManageStore(session.StoreId) || session.OpenedByUserId == userId))
            return;
        if (!inspectOnly && session.OpenedByUserId == userId)
            return;
        if (!inspectOnly && _tenantAccess.CanManageStore(session.StoreId))
            return;
        throw new UnauthorizedAccessException("Unauthorized cash register session.");
    }

    private int RequireUser() =>
        _tenantAccess.CurrentUserId ?? throw new UnauthorizedAccessException("Invalid user claim.");

    private int ResolveStore(int? storeId)
    {
        var resolved = storeId ?? _tenantAccess.SelectedStoreId;
        if (!resolved.HasValue)
            throw new InvalidOperationException("A selected store is required.");
        return resolved.Value;
    }

    private static string? NormalizeMovementType(string? movementType)
    {
        if (string.Equals(movementType, "CashIn", StringComparison.OrdinalIgnoreCase))
            return "CashIn";
        if (string.Equals(movementType, "CashOut", StringComparison.OrdinalIgnoreCase))
            return "CashOut";
        return null;
    }
}
