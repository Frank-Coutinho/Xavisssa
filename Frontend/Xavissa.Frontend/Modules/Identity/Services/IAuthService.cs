using System;
using System.Collections.Generic;
using Xavissa.Frontend.Data.Entities;
using Xavissa.Frontend.Models.Auth;

namespace Xavissa.Frontend.Services
{
    public interface IAuthService
    {
        OfflineIdentity? CurrentUser { get; }
        bool IsLoggedIn { get; }
        int? CurrentUserId { get; }
        string Username { get; }
        string PlatformRole { get; }
        string ActingRole { get; }
        bool IsAuthenticated { get; }
        bool IsOnlineSession { get; }
        bool IsOfflineSession { get; }
        bool HasSelectedStore { get; }
        bool IsTenantAdmin { get; }
        bool IsStoreManager { get; }
        bool IsClerk { get; }
        bool IsClerkOrCashier { get; }
        bool IsSystemAdmin { get; }
        bool IsSupport { get; }
        bool CanManageStores { get; }
        bool CanManageEmployees { get; }
        bool CanManageUsers { get; }
        bool CanManageCatalog { get; }
        bool CanManageVariants { get; }
        bool CanManageStock { get; }
        bool CanEditTenantPrinting { get; }
        bool CanEditStorePrinting { get; }
        bool CanViewTenantAnalytics { get; }
        bool CanViewStoreAnalytics { get; }
        bool CanPerformSales { get; }
        bool CanUsePOS { get; }
        bool CanViewHistory { get; }
        bool CanRefundSales { get; }
        bool CanVoidSales { get; }
        bool CanUseCashRegister { get; }
        bool CanManageCashRegisterSettings { get; }
        bool CanViewAnalytics { get; }
        bool CanViewAuditLogs { get; }
        bool CanViewSyncConflicts { get; }
        bool CanManageLicensing { get; }
        bool CanUsePlatformAdmin { get; }
        bool CanPrintBarcodeLabels { get; }
        int? SelectedTenantId { get; }
        int? SelectedStoreId { get; }
        string SelectedStoreRole { get; }
        IReadOnlyList<AssignedTenant> AllowedTenants { get; }
        IReadOnlyList<AssignedStore> AllowedStores { get; }
        event Action? UserChanged;
        event Action? SessionExpired;
        void StartSession(OfflineIdentity user);
        void ApplyOnlineSession(LoginResponse login);
        bool SetSelectedStore(int storeId);
        void NotifySessionExpired();
        void Logout();
    }
}
