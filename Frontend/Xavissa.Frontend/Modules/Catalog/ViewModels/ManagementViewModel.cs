using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Threading.Tasks;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using Xavissa.Frontend.Auth.Common;
using Xavissa.Frontend.Data.Repositories;
using Xavissa.Frontend.Models;
using Xavissa.Frontend.Models.Auth;
using Xavissa.Frontend.Services;

namespace Xavissa.Frontend.ViewModels
{
    public enum TeamUserFilter
    {
        All,
        Active,
        Inactive,
    }

    public class ManagementViewModel : ViewModelBase
    {
        private static readonly string[] TenantAdminUserRoles = [AppRoles.StoreManager, AppRoles.Clerk];
        private static readonly string[] StoreManagerUserRoles = [AppRoles.Clerk];

        private readonly IUserRepository _userRepo;
        private readonly IProductRepository _productRepo;
        private readonly IStoreAdminRepository _storeAdminRepo;
        private readonly IConnectivityService _net;
        private readonly INotificationService _notify;
        private readonly IAuthService _auth;
        private readonly IApiTokenProvider _tokens;
        private readonly IPrinterService _printer;
        private readonly ILocalizationService _localization;
        private readonly IConfirmationDialogService _confirmations;
        private readonly ITenantQuotaService _tenantQuota;
        private readonly ILicenseFeatureGate _featureGate;

        public Interaction<Unit, Unit> NavigateToLogin { get; } = new();

        public ObservableCollection<User> Users { get; } = new();
        public ObservableCollection<Product> Products { get; } = new();
        public ObservableCollection<CatalogCategory> Categories { get; } = new();
        public ObservableCollection<StoreRecord> Stores { get; } = new();
        public ObservableCollection<UserStoreAssignment> SelectedUserStores { get; } = new();
        public ObservableCollection<ProductStoreAssignment> SelectedProductStores { get; } = new();
        public ObservableCollection<ProductVariantRecord> SelectedProductVariants { get; } = new();
        public ObservableCollection<ProductVariantRecord> StoreProductVariants { get; } = new();

        private TeamUserFilter _selectedTeamUserFilter = TeamUserFilter.All;
        public TeamUserFilter SelectedTeamUserFilter
        {
            get => _selectedTeamUserFilter;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedTeamUserFilter, value);
                this.RaisePropertyChanged(nameof(IsAllUsersFilterSelected));
                this.RaisePropertyChanged(nameof(IsActiveUsersFilterSelected));
                this.RaisePropertyChanged(nameof(IsInactiveUsersFilterSelected));
                this.RaisePropertyChanged(nameof(VisibleUsers));
                this.RaisePropertyChanged(nameof(AllVisibleUsersSelected));
            }
        }

        private string _teamUserSearchText = string.Empty;
        public string TeamUserSearchText
        {
            get => _teamUserSearchText;
            set
            {
                this.RaiseAndSetIfChanged(ref _teamUserSearchText, value);
                this.RaisePropertyChanged(nameof(VisibleUsers));
                this.RaisePropertyChanged(nameof(AllVisibleUsersSelected));
            }
        }

        private bool _isCreateUserPopupOpen;
        public bool IsCreateUserPopupOpen
        {
            get => _isCreateUserPopupOpen;
            set => this.RaiseAndSetIfChanged(ref _isCreateUserPopupOpen, value);
        }

        private bool _isUserEditorOpen;
        public bool IsUserEditorOpen
        {
            get => _isUserEditorOpen;
            set => this.RaiseAndSetIfChanged(ref _isUserEditorOpen, value);
        }

        private bool _isCreateStorePopupOpen;
        public bool IsCreateStorePopupOpen
        {
            get => _isCreateStorePopupOpen;
            set => this.RaiseAndSetIfChanged(ref _isCreateStorePopupOpen, value);
        }

        private bool _isCreateProductPopupOpen;
        public bool IsCreateProductPopupOpen
        {
            get => _isCreateProductPopupOpen;
            set => this.RaiseAndSetIfChanged(ref _isCreateProductPopupOpen, value);
        }

        private bool _isBarcodePrintPopupOpen;
        public bool IsBarcodePrintPopupOpen
        {
            get => _isBarcodePrintPopupOpen;
            set => this.RaiseAndSetIfChanged(ref _isBarcodePrintPopupOpen, value);
        }

        private int _selectedTabIndex;
        private bool _isLoading;
        private bool _hasLoaded;

        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                this.RaiseAndSetIfChanged(ref _isLoading, value);
                RaiseEmptyStateProperties();
            }
        }

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => this.RaiseAndSetIfChanged(ref _selectedTabIndex, value);
        }

        private bool _isProductGridView = true;
        private bool _isLoadingSelectedUserStores;
        public bool IsProductGridView
        {
            get => _isProductGridView;
            set
            {
                if (_isProductGridView == value)
                    return;

                this.RaiseAndSetIfChanged(ref _isProductGridView, value);
                this.RaisePropertyChanged(nameof(IsProductListView));
            }
        }

        public bool IsProductListView => !IsProductGridView;

        public bool IsTenantAdmin => _auth.IsTenantAdmin;
        public bool IsStoreManager => _auth.IsStoreManager;
        public bool CanManageStores => _auth.CanManageStores;
        public bool CanManageEmployees => _auth.CanManageEmployees;
        public bool CanManageCatalog => _auth.CanManageCatalog;
        public bool CanCreateBaseProducts => IsTenantAdmin;
        public bool CanEditCatalogProducts => IsTenantAdmin;
        public bool CanManageVariants => IsStoreManager
            && _auth.SelectedStoreId.HasValue
            && SelectedProduct != null;
        public bool HasCurrentStoreAssignment => _auth.SelectedStoreId.HasValue
            && (
                SelectedProductStores.Any(assignment => assignment.StoreId == _auth.SelectedStoreId.Value && assignment.IsActive)
                || SelectedProductVariants.Any(variant => variant.StoreId == _auth.SelectedStoreId.Value && variant.IsActive)
            );
        public bool CanManageAssignments => _auth.CanManageEmployees;
        public bool ShowEmployeeTabs => CanManageEmployees;
        public bool ShowStoreTab => CanManageStores;
        public bool ShowCatalogTabs => CanManageCatalog;
        public bool ShowCategoriesTab => IsTenantAdmin && CanManageCatalog;
        public bool ShowVariantManagementTab => IsStoreManager && CanManageCatalog;
        public bool CanManageSelectedUserStores => CanManageAssignments && SelectedUser != null;
        public bool HasStoreScope => _auth.SelectedStoreId.HasValue;
        public bool ShowUsersEmptyState => !IsLoading && CanManageEmployees && !VisibleUsers.Any();
        public bool ShowStoresEmptyState => !IsLoading && ShowStoreTab && Stores.Count == 0;
        public bool ShowCategoriesEmptyState => !IsLoading && ShowCategoriesTab && Categories.Count == 0;
        public bool ShowCatalogEmptyState => !IsLoading && ShowCatalogTabs && Products.Count == 0;
        public bool ShowVariantsEmptyState => !IsLoading && ShowVariantManagementTab && StoreProductVariants.Count == 0;
        public string EmptyUsersText => _localization.GetString("Loc.NoUsersFound");
        public string EmptyStoresText => _localization.GetString("Loc.NoStoresFound");
        public string EmptyCategoriesText => _localization.GetString("Loc.NoCategoriesFound");
        public string EmptyCatalogText => _localization.GetString("Loc.NoCatalogProductsFound");
        public string EmptyVariantsText => _localization.GetString("Loc.NoVariantsFound");
        public bool HasSelectedUsers => SelectedUsers.Any();
        public bool HasSelectedActiveUsers => SelectedUsers.Any(user => user.IsActive);
        public bool HasSelectedInactiveUsers => SelectedUsers.Any(user => !user.IsActive);
        public bool IsAllUsersFilterSelected => SelectedTeamUserFilter == TeamUserFilter.All;
        public bool IsActiveUsersFilterSelected => SelectedTeamUserFilter == TeamUserFilter.Active;
        public bool IsInactiveUsersFilterSelected => SelectedTeamUserFilter == TeamUserFilter.Inactive;

        public string WorkspaceTitle => IsTenantAdmin
            ? _localization.GetString("Loc.ManagementWorkspaceTenantAdmin")
            : IsStoreManager
                ? _localization.GetString("Loc.ManagementWorkspaceStoreManager")
                : _localization.GetString("Loc.ManagementWorkspaceAssignedTasks");
        public string WorkspaceSubtitle => IsTenantAdmin
            ? _localization.GetString("Loc.ManagementWorkspaceSubtitleTenant")
            : IsStoreManager
                ? _localization.GetString("Loc.ManagementWorkspaceSubtitleStore")
                : _localization.GetString("Loc.ManagementWorkspaceSubtitleAssigned");

        public string CreateUserHelpText => IsTenantAdmin
            ? _localization.GetString("Loc.CreateUserHelpTenant")
            : _localization.GetString("Loc.CreateUserHelpStore");

        public string CatalogHelpText => IsTenantAdmin
            ? _localization.GetString("Loc.CatalogHelpTenant")
            : _localization.GetString("Loc.CatalogHelpStore");
        public string TeamTabDescription => IsTenantAdmin
            ? _localization.GetString("Loc.TeamTabDescriptionTenant")
            : _localization.GetString("Loc.TeamTabDescriptionStore");
        public string StoresTabDescription => IsTenantAdmin
            ? _localization.GetString("Loc.StoresTabDescriptionTenant")
            : _localization.GetString("Loc.StoresTabDescriptionStore");
        public string CatalogTabDescription => IsTenantAdmin
            ? _localization.GetString("Loc.CatalogTabDescriptionTenant")
            : _localization.GetString("Loc.CatalogTabDescriptionStore");
        public string CategoriesTabHeader => _localization.GetString("Loc.Categories");
        public string CategoriesTabDescription => _localization.GetString("Loc.CategoriesHelp");
        public string VariantManagementDescription => _localization.GetString("Loc.VariantManagementDescription");
        public string TeamTabHeader => IsTenantAdmin || IsStoreManager ? _localization.GetString("Loc.Users") : _localization.GetString("Loc.ManageUsers");
        public string StoreTabHeader => IsTenantAdmin ? _localization.GetString("Loc.Stores") : _localization.GetString("Loc.ManageStores");
        public string CatalogTabHeader => IsTenantAdmin ? _localization.GetString("Loc.Products") : _localization.GetString("Loc.StoreProducts");
        public string VariantManagementTabHeader => IsTenantAdmin ? _localization.GetString("Loc.Products") : _localization.GetString("Loc.ProductVariants");
        public string StoreProductVariantSummary => StoreProductVariants.Count == 0
            ? _localization.GetString("Loc.NoSellableVariantsYet")
            : string.Format(CultureInfo.CurrentCulture, StoreProductVariants.Count == 1 ? _localization.GetString("Loc.OneSellableVariantAvailable") : _localization.GetString("Loc.ManySellableVariantsAvailable"), StoreProductVariants.Count);
        public string SelectedUserStoreSummary => SelectedUserStores.Count == 0
            ? _localization.GetString("Loc.NoStoreAssignmentsYet")
            : string.Format(CultureInfo.CurrentCulture, SelectedUserStores.Count == 1 ? _localization.GetString("Loc.OneStoreAssignmentConfigured") : _localization.GetString("Loc.ManyStoreAssignmentsConfigured"), SelectedUserStores.Count);
        public string SelectedProductStoreSummary => SelectedProductStores.Count == 0
            ? _localization.GetString("Loc.NotAssignedToAnyStoreYet")
            : string.Format(CultureInfo.CurrentCulture, SelectedProductStores.Count == 1 ? _localization.GetString("Loc.OneStoreAssignmentConfigured") : _localization.GetString("Loc.ManyStoreAssignmentsConfigured"), SelectedProductStores.Count);
        public string CurrentStoreAssignmentSummary => HasCurrentStoreAssignment
            ? _localization.GetString("Loc.AlreadyAssignedToCurrentStore")
            : _localization.GetString("Loc.NotYetAssignedToCurrentStore");
        public string SelectedProductVariantSummary => SelectedProductVariants.Count == 0
            ? _localization.GetString("Loc.NoSellableVariantsCreated")
            : string.Format(CultureInfo.CurrentCulture, SelectedProductVariants.Count == 1 ? _localization.GetString("Loc.OneSellableVariantConfigured") : _localization.GetString("Loc.ManySellableVariantsConfigured"), SelectedProductVariants.Count);
        public string TeamSelectionSummary => SelectedUsers.Count() == 0
            ? _localization.GetString("Loc.SelectTeamMembersBulkActions")
            : string.Format(CultureInfo.CurrentCulture, SelectedUsers.Count() == 1 ? _localization.GetString("Loc.OneTeamMemberSelected") : _localization.GetString("Loc.ManyTeamMembersSelected"), SelectedUsers.Count());

        private string _newUsername = string.Empty;
        public string NewUsername
        {
            get => _newUsername;
            set => this.RaiseAndSetIfChanged(ref _newUsername, value);
        }

        private string _newEmail = string.Empty;
        public string NewEmail
        {
            get => _newEmail;
            set => this.RaiseAndSetIfChanged(ref _newEmail, value);
        }

        private string _newPassword = string.Empty;
        public string NewPassword
        {
            get => _newPassword;
            set => this.RaiseAndSetIfChanged(ref _newPassword, value);
        }

        private string _newUserRole;
        public string NewUserRole
        {
            get => _newUserRole;
            set => this.RaiseAndSetIfChanged(ref _newUserRole, value);
        }

        private StoreRecord? _newUserStore;
        public StoreRecord? NewUserStore
        {
            get => _newUserStore;
            set => this.RaiseAndSetIfChanged(ref _newUserStore, value);
        }

        private string _newProductName = string.Empty;
        public string NewProductName
        {
            get => _newProductName;
            set => this.RaiseAndSetIfChanged(ref _newProductName, value);
        }

        private string _newProductCategory = string.Empty;
        public string NewProductCategory
        {
            get => _newProductCategory;
            set => this.RaiseAndSetIfChanged(ref _newProductCategory, value);
        }

        private CatalogCategory? _selectedNewProductCategory;
        public CatalogCategory? SelectedNewProductCategory
        {
            get => _selectedNewProductCategory;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedNewProductCategory, value);
                if (value != null)
                    NewProductCategory = value.Name;
            }
        }

        private string _newCategoryName = string.Empty;
        public string NewCategoryName
        {
            get => _newCategoryName;
            set => this.RaiseAndSetIfChanged(ref _newCategoryName, value);
        }

        private CatalogCategory? _selectedEditProductCategory;
        public CatalogCategory? SelectedEditProductCategory
        {
            get => _selectedEditProductCategory;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedEditProductCategory, value);
                if (SelectedProduct != null && value != null)
                {
                    SelectedProduct.CategoryId = value.Id;
                    SelectedProduct.Category = value.Name;
                }
            }
        }

        private string _newProductBrand = string.Empty;
        public string NewProductBrand
        {
            get => _newProductBrand;
            set => this.RaiseAndSetIfChanged(ref _newProductBrand, value);
        }

        private string _newProductCode = string.Empty;
        public string NewProductCode
        {
            get => _newProductCode;
            set => this.RaiseAndSetIfChanged(ref _newProductCode, value);
        }

        private string _newProductDescription = string.Empty;
        public string NewProductDescription
        {
            get => _newProductDescription;
            set => this.RaiseAndSetIfChanged(ref _newProductDescription, value);
        }

        private bool _newProductIsActive = true;
        public bool NewProductIsActive
        {
            get => _newProductIsActive;
            set => this.RaiseAndSetIfChanged(ref _newProductIsActive, value);
        }

        private string _newProductSku = string.Empty;
        public string NewProductSku
        {
            get => _newProductSku;
            set => this.RaiseAndSetIfChanged(ref _newProductSku, value);
        }

        private string _newProductLabel = string.Empty;
        public string NewProductLabel
        {
            get => _newProductLabel;
            set => this.RaiseAndSetIfChanged(ref _newProductLabel, value);
        }

        private string _newProductColor = string.Empty;
        public string NewProductColor
        {
            get => _newProductColor;
            set => this.RaiseAndSetIfChanged(ref _newProductColor, value);
        }

        private string _newProductSize = string.Empty;
        public string NewProductSize
        {
            get => _newProductSize;
            set => this.RaiseAndSetIfChanged(ref _newProductSize, value);
        }

        private decimal _newProductPrice;
        public decimal NewProductPrice
        {
            get => _newProductPrice;
            set => this.RaiseAndSetIfChanged(ref _newProductPrice, value);
        }

        private int _newProductQuantity;
        public int NewProductQuantity
        {
            get => _newProductQuantity;
            set => this.RaiseAndSetIfChanged(ref _newProductQuantity, value);
        }

        private string _newStoreName = string.Empty;
        public string NewStoreName
        {
            get => _newStoreName;
            set => this.RaiseAndSetIfChanged(ref _newStoreName, value);
        }

        private bool _newStoreIsActive = true;
        public bool NewStoreIsActive
        {
            get => _newStoreIsActive;
            set => this.RaiseAndSetIfChanged(ref _newStoreIsActive, value);
        }

        private User? _selectedUser;
        public User? SelectedUser
        {
            get => _selectedUser;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedUser, value);
                this.RaisePropertyChanged(nameof(CanManageSelectedUserStores));
                _ = LoadSelectedUserStoresAsync();
            }
        }

        private Product? _selectedProduct;
        public Product? SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedProduct, value);
                this.RaisePropertyChanged(nameof(CanManageVariants));
            }
        }

        private StoreRecord? _selectedAssignableStore;
        public StoreRecord? SelectedAssignableStore
        {
            get => _selectedAssignableStore;
            set => this.RaiseAndSetIfChanged(ref _selectedAssignableStore, value);
        }

        private string _selectedStoreRole;
        public string SelectedStoreRole
        {
            get => _selectedStoreRole;
            set => this.RaiseAndSetIfChanged(ref _selectedStoreRole, value);
        }

        private StoreRecord? _selectedProductAssignmentStore;
        public StoreRecord? SelectedProductAssignmentStore
        {
            get => _selectedProductAssignmentStore;
            set => this.RaiseAndSetIfChanged(ref _selectedProductAssignmentStore, value);
        }

        private decimal _selectedProductAssignmentPrice;
        public decimal SelectedProductAssignmentPrice
        {
            get => _selectedProductAssignmentPrice;
            set => this.RaiseAndSetIfChanged(ref _selectedProductAssignmentPrice, value);
        }

        private int _selectedProductAssignmentQuantity;
        public int SelectedProductAssignmentQuantity
        {
            get => _selectedProductAssignmentQuantity;
            set => this.RaiseAndSetIfChanged(ref _selectedProductAssignmentQuantity, value);
        }

        private ProductVariantRecord? _selectedProductVariant;
        public ProductVariantRecord? SelectedProductVariant
        {
            get => _selectedProductVariant;
            set => this.RaiseAndSetIfChanged(ref _selectedProductVariant, value);
        }

        private bool _isEditingStoreVariant;
        public bool IsEditingStoreVariant
        {
            get => _isEditingStoreVariant;
            set => this.RaiseAndSetIfChanged(ref _isEditingStoreVariant, value);
        }

        public string StoreManagerVariantPopupTitle => IsEditingStoreVariant
            ? _localization.GetString("Loc.EditStoreVariant")
            : _localization.GetString("Loc.CreateStoreVariant");

        private string _variantLabel = string.Empty;
        public string VariantLabel
        {
            get => _variantLabel;
            set => this.RaiseAndSetIfChanged(ref _variantLabel, value);
        }

        private string _variantSku = string.Empty;
        public string VariantSku
        {
            get => _variantSku;
            set => this.RaiseAndSetIfChanged(ref _variantSku, value);
        }

        private string _variantBarcode = string.Empty;
        public string VariantBarcode
        {
            get => _variantBarcode;
            set => this.RaiseAndSetIfChanged(ref _variantBarcode, value);
        }

        private decimal _variantPrice;
        public decimal VariantPrice
        {
            get => _variantPrice;
            set
            {
                this.RaiseAndSetIfChanged(ref _variantPrice, value);
                VariantPriceText = value > 0 ? value.ToString(CultureInfo.CurrentCulture) : string.Empty;
            }
        }

        private decimal _variantCostPrice;
        public decimal VariantCostPrice
        {
            get => _variantCostPrice;
            set => this.RaiseAndSetIfChanged(ref _variantCostPrice, value);
        }

        private int _variantStockQuantity;
        public int VariantStockQuantity
        {
            get => _variantStockQuantity;
            set
            {
                this.RaiseAndSetIfChanged(ref _variantStockQuantity, value);
                VariantStockQuantityText = value > 0 ? value.ToString(CultureInfo.CurrentCulture) : string.Empty;
            }
        }

        private string _variantPriceText = string.Empty;
        public string VariantPriceText
        {
            get => _variantPriceText;
            set
            {
                this.RaiseAndSetIfChanged(ref _variantPriceText, value);

                if (TryParseDecimal(value, out var parsedPrice))
                    this.RaiseAndSetIfChanged(ref _variantPrice, parsedPrice, nameof(VariantPrice));
                else if (string.IsNullOrWhiteSpace(value))
                    this.RaiseAndSetIfChanged(ref _variantPrice, 0m, nameof(VariantPrice));
            }
        }

        private string _variantStockQuantityText = string.Empty;
        public string VariantStockQuantityText
        {
            get => _variantStockQuantityText;
            set
            {
                this.RaiseAndSetIfChanged(ref _variantStockQuantityText, value);

                if (TryParseInt(value, out var parsedQuantity))
                    this.RaiseAndSetIfChanged(ref _variantStockQuantity, parsedQuantity, nameof(VariantStockQuantity));
                else if (string.IsNullOrWhiteSpace(value))
                    this.RaiseAndSetIfChanged(ref _variantStockQuantity, 0, nameof(VariantStockQuantity));
            }
        }

        private bool _variantIsActive = true;
        public bool VariantIsActive
        {
            get => _variantIsActive;
            set => this.RaiseAndSetIfChanged(ref _variantIsActive, value);
        }

        private int _barcodePrintQuantity = 1;
        public int BarcodePrintQuantity
        {
            get => _barcodePrintQuantity;
            set => this.RaiseAndSetIfChanged(ref _barcodePrintQuantity, value < 1 ? 1 : value);
        }

        private string _barcodePrintQuantityText = "1";
        public string BarcodePrintQuantityText
        {
            get => _barcodePrintQuantityText;
            set
            {
                this.RaiseAndSetIfChanged(ref _barcodePrintQuantityText, value);

                if (TryParseInt(value, out var parsedQuantity))
                    this.RaiseAndSetIfChanged(ref _barcodePrintQuantity, parsedQuantity < 1 ? 1 : parsedQuantity, nameof(BarcodePrintQuantity));
            }
        }

        private ProductVariantRecord? _barcodePrintVariant;
        public ProductVariantRecord? BarcodePrintVariant
        {
            get => _barcodePrintVariant;
            set => this.RaiseAndSetIfChanged(ref _barcodePrintVariant, value);
        }

        private string _barcodePrintProductName = string.Empty;
        public string BarcodePrintProductName
        {
            get => _barcodePrintProductName;
            set => this.RaiseAndSetIfChanged(ref _barcodePrintProductName, value);
        }

        public IEnumerable<string> AvailableUserRoles => IsTenantAdmin ? TenantAdminUserRoles : StoreManagerUserRoles;
        public IEnumerable<string> AvailableStoreRoles => AvailableUserRoles;
        public IEnumerable<CatalogCategory> AvailableCategories => Categories
            .Where(category => category.IsActive)
            .OrderBy(category => category.Name)
            .ToList();
        public IEnumerable<StoreRecord> AvailableNewUserStores => Stores;
        public IEnumerable<User> VisibleUsers => Users.Where(MatchesTeamUserFilter).ToList();
        public IEnumerable<User> SelectedUsers => Users.Where(user => user.IsSelected).ToList();
        public IEnumerable<StoreRecord> AvailableAssignableStores => Stores.Where(store =>
            SelectedUserStores.All(assignment => assignment.StoreId != store.Id));
        public IEnumerable<StoreRecord> AvailableProductAssignmentStores => Stores.Where(store =>
            SelectedProductStores.All(assignment => assignment.StoreId != store.Id));
        public bool AllVisibleUsersSelected
        {
            get
            {
                var visible = VisibleUsers.ToList();
                return visible.Count > 0 && visible.All(user => user.IsSelected);
            }
            set
            {
                foreach (var user in VisibleUsers)
                    user.IsSelected = value;

                RefreshTeamUserListState();
            }
        }

        public event EventHandler? ShowEditProductRequested;

        public ReactiveCommand<Unit, Unit> LoadCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenCreateUserPopupCommand { get; }
        public ReactiveCommand<Unit, Unit> CloseCreateUserPopupCommand { get; }
        public ReactiveCommand<User, Unit> OpenUserEditorCommand { get; }
        public ReactiveCommand<Unit, Unit> CloseUserEditorCommand { get; }
        public ReactiveCommand<Unit, Unit> CreateUserCommand { get; }
        public ReactiveCommand<User, Unit> DeleteUserCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenCreateProductPopupCommand { get; }
        public ReactiveCommand<Unit, Unit> CloseCreateProductPopupCommand { get; }
        public ReactiveCommand<Unit, Unit> CreateProductCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveCategoryCommand { get; }
        public ReactiveCommand<CatalogCategory, Unit> DeleteCategoryCommand { get; }
        public ReactiveCommand<Product, Unit> DeleteProductCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveEditProductCommand { get; }
        public ReactiveCommand<Product, Unit> OpenEditProductPopupCommand { get; }
        public ReactiveCommand<Unit, Unit> CancelEditProductCommand { get; }
        public ReactiveCommand<Unit, Unit> SetProductGridViewCommand { get; }
        public ReactiveCommand<Unit, Unit> SetProductListViewCommand { get; }
        public ReactiveCommand<Unit, Unit> LogoutCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenCreateStorePopupCommand { get; }
        public ReactiveCommand<Unit, Unit> CloseCreateStorePopupCommand { get; }
        public ReactiveCommand<Unit, Unit> CreateStoreCommand { get; }
        public ReactiveCommand<StoreRecord, Unit> ToggleStoreStatusCommand { get; }
        public ReactiveCommand<StoreRecord, Unit> DeactivateStoreCommand { get; }
        public ReactiveCommand<StoreRecord, Unit> DeleteStoreCommand { get; }
        public ReactiveCommand<Unit, Unit> AssignSelectedStoreCommand { get; }
        public ReactiveCommand<UserStoreAssignment, Unit> RemoveUserStoreCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveProductStoreAssignmentCommand { get; }
        public ReactiveCommand<ProductStoreAssignment, Unit> RemoveProductStoreAssignmentCommand { get; }
        public ReactiveCommand<Unit, Unit> ShowAllUsersCommand { get; }
        public ReactiveCommand<Unit, Unit> ShowActiveUsersCommand { get; }
        public ReactiveCommand<Unit, Unit> ShowInactiveUsersCommand { get; }
        public ReactiveCommand<User, Unit> MarkUserActiveCommand { get; }
        public ReactiveCommand<User, Unit> MarkUserInactiveCommand { get; }
        public ReactiveCommand<Unit, Unit> MarkSelectedUsersActiveCommand { get; }
        public ReactiveCommand<Unit, Unit> MarkSelectedUsersInactiveCommand { get; }
        public ReactiveCommand<Unit, Unit> DeleteSelectedUsersCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveProductVariantCommand { get; }
        public ReactiveCommand<ProductVariantRecord, Unit> EditProductVariantCommand { get; }
        public ReactiveCommand<ProductVariantRecord, Unit> OpenStoreProductVariantCommand { get; }
        public ReactiveCommand<ProductVariantRecord, Unit> PrintStoreProductVariantCommand { get; }
        public ReactiveCommand<Product, Unit> OpenCreateVariantPopupCommand { get; }
        public ReactiveCommand<ProductVariantRecord, Unit> DeleteProductVariantCommand { get; }
        public ReactiveCommand<Unit, Unit> GenerateVariantBarcodeCommand { get; }
        public ReactiveCommand<Unit, Unit> PrintVariantBarcodeCommand { get; }
        public ReactiveCommand<Unit, Unit> ConfirmBarcodePrintCommand { get; }
        public ReactiveCommand<Unit, Unit> CloseBarcodePrintPopupCommand { get; }
        public ReactiveCommand<Unit, Unit> CancelVariantEditCommand { get; }

        public ManagementViewModel(
            IUserRepository userRepo,
            IProductRepository productRepo,
            IStoreAdminRepository storeAdminRepo,
            IConnectivityService net,
            INotificationService notify,
            IAuthService auth,
            IApiTokenProvider tokens,
            IPrinterService printer,
            ILocalizationService localization,
            IConfirmationDialogService confirmations,
            ITenantQuotaService tenantQuota,
            ILicenseFeatureGate featureGate)
        {
            _userRepo = userRepo;
            _productRepo = productRepo;
            _storeAdminRepo = storeAdminRepo;
            _net = net;
            _notify = notify;
            _auth = auth;
            _tokens = tokens;
            _printer = printer;
            _localization = localization;
            _confirmations = confirmations;
            _tenantQuota = tenantQuota;
            _featureGate = featureGate;

            _newUserRole = AvailableUserRoles.FirstOrDefault() ?? AppRoles.Clerk;
            _selectedStoreRole = AvailableStoreRoles.FirstOrDefault() ?? AppRoles.Clerk;

            LoadCommand = ReactiveCommand.CreateFromTask(LoadAllAsync);
            OpenCreateUserPopupCommand = ReactiveCommand.Create(() =>
            {
                ResetNewUserForm();
                IsCreateUserPopupOpen = true;
            });
            CloseCreateUserPopupCommand = ReactiveCommand.Create(() => { IsCreateUserPopupOpen = false; });
            OpenUserEditorCommand = ReactiveCommand.CreateFromTask<User>(OpenUserEditorAsync);
            CloseUserEditorCommand = ReactiveCommand.Create(() =>
            {
                IsUserEditorOpen = false;
                SelectedUser = null;
            });
            CreateUserCommand = ReactiveCommand.CreateFromTask(CreateUserAsync);
            OpenCreateProductPopupCommand = ReactiveCommand.Create(() =>
            {
                ResetNewProductForm();
                IsCreateProductPopupOpen = true;
            });
            CloseCreateProductPopupCommand = ReactiveCommand.Create(() =>
            {
                ResetNewProductForm();
                IsCreateProductPopupOpen = false;
            });
            DeleteUserCommand = ReactiveCommand.CreateFromTask<User>(DeleteUserAsync);
            CreateProductCommand = ReactiveCommand.CreateFromTask(CreateProductAsync);
            SaveCategoryCommand = ReactiveCommand.CreateFromTask(SaveCategoryAsync);
            DeleteCategoryCommand = ReactiveCommand.CreateFromTask<CatalogCategory>(DeleteCategoryAsync);
            DeleteProductCommand = ReactiveCommand.CreateFromTask<Product>(DeleteProductAsync);
            SaveEditProductCommand = ReactiveCommand.CreateFromTask(SaveEditProductAsync);
            OpenCreateStorePopupCommand = ReactiveCommand.Create(() =>
            {
                NewStoreName = string.Empty;
                NewStoreIsActive = true;
                IsCreateStorePopupOpen = true;
            });
            CloseCreateStorePopupCommand = ReactiveCommand.Create(() => { IsCreateStorePopupOpen = false; });
            CreateStoreCommand = ReactiveCommand.CreateFromTask(CreateStoreAsync);
            ToggleStoreStatusCommand = ReactiveCommand.CreateFromTask<StoreRecord>(ToggleStoreStatusAsync);
            DeactivateStoreCommand = ReactiveCommand.CreateFromTask<StoreRecord>(DeactivateStoreAsync);
            DeleteStoreCommand = ReactiveCommand.CreateFromTask<StoreRecord>(DeleteStoreAsync);
            AssignSelectedStoreCommand = ReactiveCommand.CreateFromTask(AssignSelectedStoreAsync);
            RemoveUserStoreCommand = ReactiveCommand.CreateFromTask<UserStoreAssignment>(RemoveUserStoreAsync);
            SaveProductStoreAssignmentCommand = ReactiveCommand.CreateFromTask(SaveProductStoreAssignmentAsync);
            RemoveProductStoreAssignmentCommand = ReactiveCommand.CreateFromTask<ProductStoreAssignment>(RemoveProductStoreAssignmentAsync);
            ShowAllUsersCommand = ReactiveCommand.Create(() => { SelectedTeamUserFilter = TeamUserFilter.All; });
            ShowActiveUsersCommand = ReactiveCommand.Create(() => { SelectedTeamUserFilter = TeamUserFilter.Active; });
            ShowInactiveUsersCommand = ReactiveCommand.Create(() => { SelectedTeamUserFilter = TeamUserFilter.Inactive; });
            MarkUserActiveCommand = ReactiveCommand.CreateFromTask<User>(MarkUserActiveAsync);
            MarkUserInactiveCommand = ReactiveCommand.CreateFromTask<User>(MarkUserInactiveAsync);
            MarkSelectedUsersActiveCommand = ReactiveCommand.CreateFromTask(MarkSelectedUsersActiveAsync);
            MarkSelectedUsersInactiveCommand = ReactiveCommand.CreateFromTask(MarkSelectedUsersInactiveAsync);
            DeleteSelectedUsersCommand = ReactiveCommand.CreateFromTask(DeleteSelectedUsersAsync);
            SaveProductVariantCommand = ReactiveCommand.CreateFromTask(SaveProductVariantAsync);
            EditProductVariantCommand = ReactiveCommand.Create<ProductVariantRecord>(EditProductVariant);
            OpenStoreProductVariantCommand = ReactiveCommand.CreateFromTask<ProductVariantRecord>(OpenStoreProductVariantAsync);
            PrintStoreProductVariantCommand = ReactiveCommand.CreateFromTask<ProductVariantRecord>(PrintStoreProductVariantAsync);
            OpenCreateVariantPopupCommand = ReactiveCommand.CreateFromTask<Product>(OpenCreateVariantPopupAsync);
            DeleteProductVariantCommand = ReactiveCommand.CreateFromTask<ProductVariantRecord>(DeleteProductVariantAsync);
            GenerateVariantBarcodeCommand = ReactiveCommand.CreateFromTask(GenerateVariantBarcodeAsync);
            PrintVariantBarcodeCommand = ReactiveCommand.CreateFromTask(PrintVariantBarcodeAsync);
            ConfirmBarcodePrintCommand = ReactiveCommand.CreateFromTask(ConfirmBarcodePrintAsync);
            CloseBarcodePrintPopupCommand = ReactiveCommand.Create(CloseBarcodePrintPopup);
            CancelVariantEditCommand = ReactiveCommand.Create(ResetVariantEditor);

            OpenEditProductPopupCommand = ReactiveCommand.CreateFromTask<Product>(OpenEditProductAsync);
            SetProductGridViewCommand = ReactiveCommand.Create(() => { IsProductGridView = true; });
            SetProductListViewCommand = ReactiveCommand.Create(() => { IsProductGridView = false; });
            CancelEditProductCommand = ReactiveCommand.Create(() =>
            {
                SelectedProduct = null;
                SelectedEditProductCategory = null;
                IsEditingStoreVariant = false;
                SelectedProductStores.Clear();
                SelectedProductVariants.Clear();
                SelectedProductAssignmentStore = null;
                ResetVariantEditor();
                this.RaisePropertyChanged(nameof(SelectedProductStoreSummary));
                this.RaisePropertyChanged(nameof(SelectedProductVariantSummary));
                this.RaisePropertyChanged(nameof(AvailableProductAssignmentStores));
            });
            LogoutCommand = ReactiveCommand.CreateFromTask(async () => { await NavigateToLogin.Handle(Unit.Default).ToTask(); });

            HandleErrors(
                LoadCommand,
                OpenCreateUserPopupCommand,
                CloseCreateUserPopupCommand,
                OpenUserEditorCommand,
                CloseUserEditorCommand,
                CreateUserCommand,
                DeleteUserCommand,
                OpenCreateProductPopupCommand,
                CloseCreateProductPopupCommand,
                CreateProductCommand,
                SaveCategoryCommand,
                DeleteCategoryCommand,
                DeleteProductCommand,
                SaveEditProductCommand,
                OpenEditProductPopupCommand,
                SetProductGridViewCommand,
                SetProductListViewCommand,
                CancelEditProductCommand,
                LogoutCommand,
                OpenCreateStorePopupCommand,
                CloseCreateStorePopupCommand,
                CreateStoreCommand,
                ToggleStoreStatusCommand,
                DeactivateStoreCommand,
                DeleteStoreCommand,
                AssignSelectedStoreCommand,
                RemoveUserStoreCommand,
                SaveProductStoreAssignmentCommand,
                RemoveProductStoreAssignmentCommand,
                ShowAllUsersCommand,
                ShowActiveUsersCommand,
                ShowInactiveUsersCommand,
                MarkUserActiveCommand,
                MarkUserInactiveCommand,
                MarkSelectedUsersActiveCommand,
                MarkSelectedUsersInactiveCommand,
                DeleteSelectedUsersCommand,
                SaveProductVariantCommand,
                EditProductVariantCommand,
                OpenStoreProductVariantCommand,
                PrintStoreProductVariantCommand,
                OpenCreateVariantPopupCommand,
                DeleteProductVariantCommand,
                GenerateVariantBarcodeCommand,
                PrintVariantBarcodeCommand,
                ConfirmBarcodePrintCommand,
                CloseBarcodePrintPopupCommand,
                CancelVariantEditCommand);

            _auth.UserChanged += OnUserChanged;
            _localization.LanguageChanged += OnLanguageChanged;
        }

        public async Task EnsureLoadedAsync()
        {
            if (_hasLoaded)
                return;

            await LoadAllAsync();
        }

        private void OnUserChanged()
        {
            this.RaisePropertyChanged(nameof(IsTenantAdmin));
            this.RaisePropertyChanged(nameof(IsStoreManager));
            this.RaisePropertyChanged(nameof(CanManageStores));
            this.RaisePropertyChanged(nameof(CanManageEmployees));
            this.RaisePropertyChanged(nameof(CanManageCatalog));
            this.RaisePropertyChanged(nameof(CanCreateBaseProducts));
            this.RaisePropertyChanged(nameof(CanEditCatalogProducts));
            this.RaisePropertyChanged(nameof(CanManageVariants));
            this.RaisePropertyChanged(nameof(HasCurrentStoreAssignment));
            this.RaisePropertyChanged(nameof(CanManageAssignments));
            this.RaisePropertyChanged(nameof(ShowEmployeeTabs));
            this.RaisePropertyChanged(nameof(ShowStoreTab));
            this.RaisePropertyChanged(nameof(ShowCatalogTabs));
            this.RaisePropertyChanged(nameof(ShowVariantManagementTab));
            this.RaisePropertyChanged(nameof(WorkspaceTitle));
            this.RaisePropertyChanged(nameof(WorkspaceSubtitle));
            this.RaisePropertyChanged(nameof(CreateUserHelpText));
            this.RaisePropertyChanged(nameof(CatalogHelpText));
            this.RaisePropertyChanged(nameof(TeamTabDescription));
            this.RaisePropertyChanged(nameof(StoresTabDescription));
            this.RaisePropertyChanged(nameof(TeamTabHeader));
            this.RaisePropertyChanged(nameof(StoreTabHeader));
            this.RaisePropertyChanged(nameof(CatalogTabHeader));
            this.RaisePropertyChanged(nameof(CatalogTabDescription));
            this.RaisePropertyChanged(nameof(ShowCategoriesTab));
            this.RaisePropertyChanged(nameof(CategoriesTabHeader));
            this.RaisePropertyChanged(nameof(CategoriesTabDescription));
            this.RaisePropertyChanged(nameof(VariantManagementTabHeader));
            this.RaisePropertyChanged(nameof(VariantManagementDescription));
            this.RaisePropertyChanged(nameof(CurrentStoreAssignmentSummary));
            this.RaisePropertyChanged(nameof(HasStoreScope));
            this.RaisePropertyChanged(nameof(AvailableUserRoles));
            this.RaisePropertyChanged(nameof(AvailableStoreRoles));
            this.RaisePropertyChanged(nameof(AvailableNewUserStores));
            this.RaisePropertyChanged(nameof(AvailableAssignableStores));
            this.RaisePropertyChanged(nameof(AvailableProductAssignmentStores));
            this.RaisePropertyChanged(nameof(AvailableCategories));
            this.RaisePropertyChanged(nameof(SelectedProductVariantSummary));
            this.RaisePropertyChanged(nameof(Categories));
            this.RaisePropertyChanged(nameof(VisibleUsers));
            this.RaisePropertyChanged(nameof(AllVisibleUsersSelected));
            this.RaisePropertyChanged(nameof(TeamSelectionSummary));
            this.RaisePropertyChanged(nameof(HasSelectedUsers));
            this.RaisePropertyChanged(nameof(HasSelectedActiveUsers));
            this.RaisePropertyChanged(nameof(HasSelectedInactiveUsers));

            NewUserRole = AvailableUserRoles.FirstOrDefault() ?? AppRoles.Clerk;
            SelectedStoreRole = AvailableStoreRoles.FirstOrDefault() ?? AppRoles.Clerk;
            if (_hasLoaded)
                LoadCommand.Execute().Subscribe();
        }

        private void OnLanguageChanged()
        {
            this.RaisePropertyChanged(nameof(WorkspaceTitle));
            this.RaisePropertyChanged(nameof(WorkspaceSubtitle));
            this.RaisePropertyChanged(nameof(CreateUserHelpText));
            this.RaisePropertyChanged(nameof(CatalogHelpText));
            this.RaisePropertyChanged(nameof(TeamTabDescription));
            this.RaisePropertyChanged(nameof(StoresTabDescription));
            this.RaisePropertyChanged(nameof(CatalogTabDescription));
            this.RaisePropertyChanged(nameof(CategoriesTabHeader));
            this.RaisePropertyChanged(nameof(CategoriesTabDescription));
            this.RaisePropertyChanged(nameof(VariantManagementDescription));
            this.RaisePropertyChanged(nameof(TeamTabHeader));
            this.RaisePropertyChanged(nameof(StoreTabHeader));
            this.RaisePropertyChanged(nameof(CatalogTabHeader));
            this.RaisePropertyChanged(nameof(VariantManagementTabHeader));
            this.RaisePropertyChanged(nameof(StoreProductVariantSummary));
            this.RaisePropertyChanged(nameof(SelectedUserStoreSummary));
            this.RaisePropertyChanged(nameof(SelectedProductStoreSummary));
            this.RaisePropertyChanged(nameof(CurrentStoreAssignmentSummary));
            this.RaisePropertyChanged(nameof(SelectedProductVariantSummary));
            this.RaisePropertyChanged(nameof(TeamSelectionSummary));
            this.RaisePropertyChanged(nameof(StoreManagerVariantPopupTitle));
        }

        private void HandleErrors(params IHandleObservableErrors[] commands)
        {
            foreach (var c in commands)
            {
                c.ThrownExceptions.Subscribe(ex =>
                {
                    Console.WriteLine(ex);
                    if (ex is ApiException apiException)
                    {
                        var type = apiException.IsPermissionDenied || apiException.IsValidationOrBusinessError
                            ? NotificationType.Warning
                            : NotificationType.Error;
                        _notify.Show(apiException.Message, type, apiException.IsServerError ? 4000 : 3000);
                        return;
                    }

                    _notify.Show(ex.Message, NotificationType.Error);
                });
            }
        }

        private async Task LoadAllAsync()
        {
            _hasLoaded = true;
            IsLoading = true;
            try
            {
                SelectedUserStores.Clear();
                SelectedProductStores.Clear();
                SelectedProductVariants.Clear();
                StoreProductVariants.Clear();

                await Task.WhenAll(
                    LoadUsersAsync(),
                    LoadCatalogAsync(),
                    LoadStoresAsync());

                this.RaisePropertyChanged(nameof(AvailableAssignableStores));
                this.RaisePropertyChanged(nameof(AvailableNewUserStores));
                this.RaisePropertyChanged(nameof(AvailableProductAssignmentStores));
                this.RaisePropertyChanged(nameof(AvailableCategories));
                this.RaisePropertyChanged(nameof(StoreProductVariantSummary));
                RefreshTeamUserListState();

                if (SelectedProduct != null)
                {
                    await LoadSelectedProductStoresAsync();
                    await LoadSelectedProductVariantsAsync();
                }
            }
            finally
            {
                IsLoading = false;
                RaiseEmptyStateProperties();
            }
        }

        private async Task LoadUsersAsync()
        {
            var selectedUserId = SelectedUser?.EffectiveOnlineUserId;
            var selectedUserIds = SelectedUsers.Select(ResolveOnlineUserId).ToHashSet();

            foreach (var user in Users)
                user.PropertyChanged -= OnUserPropertyChanged;

            Users.Clear();

            if (CanManageEmployees)
            {
                foreach (var user in (await _userRepo.GetAllAsync()).Where(IsVisibleUser))
                {
                    user.IsSelected = selectedUserIds.Contains(user.EffectiveOnlineUserId);
                    user.PropertyChanged += OnUserPropertyChanged;
                    Users.Add(user);
                }
            }

            if (selectedUserId.HasValue)
                SelectedUser = Users.FirstOrDefault(user => user.EffectiveOnlineUserId == selectedUserId.Value);

            RefreshTeamUserListState();
        }

        private async Task LoadCatalogAsync()
        {
            Products.Clear();
            Categories.Clear();

            if (!CanManageCatalog)
                return;

            foreach (var category in await _productRepo.GetCategoriesAsync())
                Categories.Add(category);

            SelectedNewProductCategory = Categories.FirstOrDefault(c => c.Name == NewProductCategory)
                ?? Categories.FirstOrDefault(c => c.Id == SelectedNewProductCategory?.Id);
            SelectedEditProductCategory = SelectedProduct == null
                ? Categories.FirstOrDefault(c => c.Id == SelectedEditProductCategory?.Id)
                : Categories.FirstOrDefault(c => c.Id == SelectedProduct.CategoryId)
                    ?? Categories.FirstOrDefault(c => string.Equals(c.Name, SelectedProduct.Category, StringComparison.OrdinalIgnoreCase));

            var selectedProductKey = SelectedProduct == null
                ? (OnlineId: 0, Id: 0)
                : (SelectedProduct.OnlineId, SelectedProduct.Id);

            var products = IsStoreManager
                ? await BuildStoreManagerProductsAsync()
                : await _productRepo.GetCatalogAsync();

            foreach (var product in products.Where(IsVisibleProduct))
                Products.Add(product);

            if (selectedProductKey.OnlineId > 0 || selectedProductKey.Id > 0)
            {
                SelectedProduct = Products.FirstOrDefault(product =>
                    (selectedProductKey.OnlineId > 0 && product.OnlineId == selectedProductKey.OnlineId)
                    || product.Id == selectedProductKey.Id);
            }

            if (IsStoreManager)
                await LoadStoreProductVariantsAsync();

            RaiseEmptyStateProperties();
        }

        private async Task LoadStoresAsync()
        {
            var selectedNewUserStoreId = NewUserStore?.Id;
            var selectedAssignableStoreId = SelectedAssignableStore?.Id;
            var selectedProductAssignmentStoreId = SelectedProductAssignmentStore?.Id;

            Stores.Clear();

            if (CanManageStores || CanManageAssignments)
            {
                foreach (var store in (await _storeAdminRepo.GetStoresAsync()).Where(IsVisibleStore))
                    Stores.Add(store);
            }

            NewUserStore = Stores.FirstOrDefault(store => store.Id == selectedNewUserStoreId)
                ?? NewUserStore;
            SelectedAssignableStore = Stores.FirstOrDefault(store => store.Id == selectedAssignableStoreId);
            SelectedProductAssignmentStore = Stores.FirstOrDefault(store => store.Id == selectedProductAssignmentStoreId);
            RaiseEmptyStateProperties();
        }

        private async Task OpenUserEditorAsync(User user)
        {
            SelectedUser = user;
            IsUserEditorOpen = true;
            await LoadSelectedUserStoresAsync();
        }

        private async Task OpenEditProductAsync(Product product)
        {
            SelectedProduct = product;
            SelectedEditProductCategory = Categories.FirstOrDefault(c => c.Id == product.CategoryId)
                ?? Categories.FirstOrDefault(c => string.Equals(c.Name, product.Category, StringComparison.OrdinalIgnoreCase));
            SelectedProductAssignmentPrice = product.Price;
            SelectedProductAssignmentQuantity = product.StockQuantity;
            await LoadSelectedProductStoresAsync();
            await LoadSelectedProductVariantsAsync();

            if (IsStoreManager)
            {
                IsEditingStoreVariant = false;
                PrefillVariantEditorFromSelectedProduct();
            }

            ShowEditProductRequested?.Invoke(this, EventArgs.Empty);
        }

        private async Task OpenCreateVariantPopupAsync(Product product)
        {
            SelectedProduct = product;
            SelectedEditProductCategory = Categories.FirstOrDefault(c => c.Id == product.CategoryId)
                ?? Categories.FirstOrDefault(c => string.Equals(c.Name, product.Category, StringComparison.OrdinalIgnoreCase));
            SelectedProductAssignmentPrice = product.Price;
            SelectedProductAssignmentQuantity = product.StockQuantity;
            IsEditingStoreVariant = false;

            await LoadSelectedProductStoresAsync();
            await LoadSelectedProductVariantsAsync();
            ResetVariantEditor();

            ShowEditProductRequested?.Invoke(this, EventArgs.Empty);
        }

        private bool IsVisibleUser(User user)
        {
            if (user == null)
                return false;

            var currentUserId = _auth.CurrentUser?.OnlineUserId > 0
                ? _auth.CurrentUser.OnlineUserId
                : _auth.CurrentUser?.Id;
            if (currentUserId.HasValue && user.EffectiveOnlineUserId == currentUserId.Value)
                return false;

            if (IsTenantAdmin)
                return !AppRoles.IsTenantAdmin(user.EffectiveRole);

            var role = user.EffectiveRole;
            return string.Equals(role, AppRoles.Clerk, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsVisibleStore(StoreRecord store)
        {
            if (store == null)
                return false;

            if (IsTenantAdmin)
                return !_auth.SelectedTenantId.HasValue || store.TenantId == _auth.SelectedTenantId.Value;

            return _auth.AllowedStores.Any(s => s.Id == store.Id);
        }

        private bool IsVisibleProduct(Product product)
        {
            if (product == null)
                return false;

            if (_auth.SelectedTenantId.HasValue)
                return product.TenantId == 0 || product.TenantId == _auth.SelectedTenantId.Value;

            return true;
        }

        private async Task<List<Product>> BuildStoreManagerProductsAsync()
        {
            if (!_auth.SelectedStoreId.HasValue)
                return new List<Product>();

            var storeId = _auth.SelectedStoreId.Value;
            var catalog = await _productRepo.GetCatalogAsync();
            var visibleProducts = new List<Product>();

            foreach (var product in catalog.Where(IsVisibleProduct))
            {
                if (product.OnlineId <= 0)
                    continue;

                var assignments = await _productRepo.GetStoreAssignmentsAsync(product.OnlineId);
                var hasActiveAssignment = assignments.Any(assignment =>
                    assignment.StoreId == storeId && assignment.IsActive);

                if (!hasActiveAssignment)
                {
                    var variants = await _productRepo.GetVariantsAsync(product.OnlineId, storeId);
                    hasActiveAssignment = variants.Any(variant => variant.IsActive);
                }

                if (hasActiveAssignment)
                    visibleProducts.Add(product);
            }

            return visibleProducts;
        }

        private async Task CreateUserAsync()
        {
            if (!CanManageEmployees)
                return;

            if (!_net.IsOnline())
            {
                _notify.Show("Internet required", NotificationType.Warning);
                return;
            }

            if (!HasAuthenticatedOnlineSession())
                return;

            if (string.IsNullOrWhiteSpace(NewUsername) || string.IsNullOrWhiteSpace(NewPassword))
            {
                _notify.Show("Username and password required", NotificationType.Warning);
                return;
            }

            var assignedRole = string.IsNullOrWhiteSpace(NewUserRole) ? AppRoles.Clerk : NewUserRole;
            if (_auth.SelectedTenantId.HasValue)
            {
                var quota = await _tenantQuota.CanCreateTenantUserAsync(_auth.SelectedTenantId.Value);
                if (!quota.Allowed)
                {
                    _notify.Show(quota.Message ?? "User could not be created.", NotificationType.Warning);
                    return;
                }
            }

            var selectedNewUserStoreId = NewUserStore?.Id;
            var targetStoreId = IsStoreManager || string.Equals(assignedRole, AppRoles.StoreManager, StringComparison.OrdinalIgnoreCase)
                ? selectedNewUserStoreId ?? _auth.SelectedStoreId
                : null;

            if (string.Equals(assignedRole, AppRoles.StoreManager, StringComparison.OrdinalIgnoreCase) && !targetStoreId.HasValue)
            {
                _notify.Show("Select the target store before creating a store manager.", NotificationType.Warning);
                return;
            }

            await _userRepo.CreateAsync(new CreateUserRequest
            {
                Username = NewUsername.Trim(),
                Email = NewEmail.Trim(),
                Password = NewPassword,
                PlatformRole = IsTenantAdmin || IsStoreManager
                    ? AppRoles.User
                    : string.IsNullOrWhiteSpace(NewUserRole) ? AppRoles.User : NewUserRole,
                AssignedRole = assignedRole,
                TenantId = _auth.SelectedTenantId,
                StoreId = targetStoreId,
            });

            ResetNewUserForm();
            IsCreateUserPopupOpen = false;

            _notify.Show("User created", NotificationType.Success);
            await LoadUsersAsync();
        }

        private async Task DeleteUserAsync(User user)
        {
            if (!CanManageEmployees || user == null)
                return;

            var confirmed = await _confirmations.ConfirmDeleteAsync(
                "Delete user?",
                $"Delete user '{user.Username}'? This action cannot be undone.");
            if (!confirmed)
                return;

            if (!_net.IsOnline())
            {
                _notify.Show("Internet required", NotificationType.Warning);
                return;
            }

            if (!HasAuthenticatedOnlineSession())
                return;

            try
            {
                await _userRepo.DeleteAsync(user.Id);
                user.PropertyChanged -= OnUserPropertyChanged;
                Users.Remove(user);
                RefreshTeamUserListState();
                _notify.Show("User deleted", NotificationType.Success);
            }
            catch (HttpRequestException ex)
                when (ex.StatusCode == HttpStatusCode.Forbidden
                    || ex.StatusCode == HttpStatusCode.Unauthorized
                    || ex.StatusCode == HttpStatusCode.NotFound)
            {
                _notify.Show("Unauthorized to delete this user.", NotificationType.Error);
            }
        }

        private async Task MarkUserActiveAsync(User user)
        {
            if (!CanManageEmployees || user == null || user.IsActive)
                return;

            await _userRepo.UpdateStatusAsync(user.Id, true);
            _notify.Show("User marked as active", NotificationType.Success);
            await LoadUsersAsync();
        }

        private async Task MarkUserInactiveAsync(User user)
        {
            if (!CanManageEmployees || user == null || !user.IsActive)
                return;

            await _userRepo.UpdateStatusAsync(user.Id, false);
            _notify.Show("User marked as inactive", NotificationType.Success);
            await LoadUsersAsync();
        }

        private async Task MarkSelectedUsersActiveAsync()
        {
            if (!CanManageEmployees)
                return;

            var selectedUsers = SelectedUsers.Where(user => !user.IsActive).ToList();
            if (selectedUsers.Count == 0)
                return;

            foreach (var user in selectedUsers)
                await _userRepo.UpdateStatusAsync(user.Id, true);

            _notify.Show("Selected users marked as active", NotificationType.Success);
            await LoadUsersAsync();
        }

        private async Task MarkSelectedUsersInactiveAsync()
        {
            if (!CanManageEmployees)
                return;

            var selectedUsers = SelectedUsers.Where(user => user.IsActive).ToList();
            if (selectedUsers.Count == 0)
                return;

            foreach (var user in selectedUsers)
                await _userRepo.UpdateStatusAsync(user.Id, false);

            _notify.Show("Selected users marked as inactive", NotificationType.Success);
            await LoadUsersAsync();
        }

        private async Task DeleteSelectedUsersAsync()
        {
            if (!CanManageEmployees)
                return;

            var selectedUsers = SelectedUsers.ToList();
            if (selectedUsers.Count == 0)
                return;

            var confirmed = await _confirmations.ConfirmDeleteAsync(
                "Delete selected users?",
                $"Delete {selectedUsers.Count} selected user(s)? This action cannot be undone.");
            if (!confirmed)
                return;

            foreach (var user in selectedUsers)
                await _userRepo.DeleteAsync(user.Id);

            _notify.Show("Selected users deleted", NotificationType.Success);
            await LoadUsersAsync();
        }

        private async Task CreateProductAsync()
        {
            if (!CanCreateBaseProducts)
                return;

            if (string.IsNullOrWhiteSpace(NewProductName))
            {
                _notify.Show("Product name is required", NotificationType.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(NewProductCategory))
            {
                _notify.Show("Category is required", NotificationType.Warning);
                return;
            }

            var product = BuildNewProduct();
            await _productRepo.AddOrUpdateAsync(product);

            ResetNewProductForm();
            IsCreateProductPopupOpen = false;

            _notify.Show("Base product saved", NotificationType.Success);
            await LoadCatalogAsync();
        }

        private Product BuildNewProduct()
        {
            return new Product
            {
                TenantId = _auth.SelectedTenantId ?? 0,
                CategoryId = SelectedNewProductCategory?.Id,
                Name = NewProductName.Trim(),
                Category = NewProductCategory.Trim(),
                Brand = NewProductBrand.Trim(),
                Code = NewProductCode.Trim(),
                Description = NewProductDescription.Trim(),
                IsActive = NewProductIsActive,
            };
        }

        private async Task SaveCategoryAsync()
        {
            if (!IsTenantAdmin)
                return;

            if (string.IsNullOrWhiteSpace(NewCategoryName))
            {
                _notify.Show("Category name is required", NotificationType.Warning);
                return;
            }

            var category = await _productRepo.SaveCategoryAsync(new CatalogCategory
            {
                TenantId = _auth.SelectedTenantId ?? 0,
                Name = NewCategoryName.Trim(),
                IsActive = true,
            });

            if (category != null)
            {
                NewProductCategory = category.Name;
                SelectedNewProductCategory = category;
                NewCategoryName = string.Empty;
                _notify.Show("Category saved", NotificationType.Success);
                await LoadCatalogAsync();
            }
        }

        private async Task DeleteCategoryAsync(CatalogCategory category)
        {
            if (!IsTenantAdmin || category == null)
                return;

            var confirmed = await _confirmations.ConfirmDeleteAsync(
                "Delete category?",
                $"Delete category '{category.Name}'? Products using it may be affected. Use Deactivate instead if you only want to hide it.");
            if (!confirmed)
                return;

            await _productRepo.DeleteCategoryAsync(category.Id);

            if (SelectedNewProductCategory?.Id == category.Id)
                SelectedNewProductCategory = null;
            if (SelectedEditProductCategory?.Id == category.Id)
                SelectedEditProductCategory = null;

            _notify.Show("Category deactivated", NotificationType.Success);
            await LoadCatalogAsync();
        }

        private async Task DeleteProductAsync(Product product)
        {
            if (!CanCreateBaseProducts || product == null)
                return;

            var confirmed = await _confirmations.ConfirmDeleteAsync(
                "Delete product?",
                $"Delete product '{product.Name}'? This action cannot be undone, and historical sales may be affected.");
            if (!confirmed)
                return;

            await _productRepo.DeleteAsync(product.OnlineId > 0 ? product.OnlineId : product.Id);
            Products.Remove(product);
            _notify.Show("Catalog item deleted", NotificationType.Success);
        }

        private async Task SaveEditProductAsync()
        {
            if (SelectedProduct == null)
                return;

            if (!IsTenantAdmin)
            {
                _notify.Show("Store managers manage variants and assignments, not base product details.", NotificationType.Warning);
                return;
            }

            SelectedProduct.TenantId = SelectedProduct.TenantId == 0 ? _auth.SelectedTenantId ?? 0 : SelectedProduct.TenantId;
            if (SelectedEditProductCategory != null)
            {
                SelectedProduct.CategoryId = SelectedEditProductCategory.Id;
                SelectedProduct.Category = SelectedEditProductCategory.Name;
            }
            SelectedProduct.StoreId = 0;
            SelectedProduct.AssignmentId = 0;
            SelectedProduct.VariantId = 0;
            SelectedProduct.Barcode = string.Empty;
            SelectedProduct.SKU = string.Empty;
            SelectedProduct.Label = string.Empty;
            SelectedProduct.Color = string.Empty;
            SelectedProduct.Size = string.Empty;
            SelectedProduct.Price = 0;
            SelectedProduct.StockQuantity = 0;

            await _productRepo.AddOrUpdateAsync(SelectedProduct);
            await LoadSelectedProductStoresAsync();
            await LoadSelectedProductVariantsAsync();
            SelectedProduct = null;
            SelectedProductStores.Clear();
            SelectedProductVariants.Clear();
            this.RaisePropertyChanged(nameof(SelectedProductStoreSummary));
            this.RaisePropertyChanged(nameof(SelectedProductVariantSummary));
            this.RaisePropertyChanged(nameof(AvailableProductAssignmentStores));
            _notify.Show("Base product updated", NotificationType.Success);
            await LoadCatalogAsync();
        }

        private void ResetNewProductForm()
        {
            NewProductName = string.Empty;
            NewProductCategory = string.Empty;
            SelectedNewProductCategory = null;
            NewCategoryName = string.Empty;
            NewProductBrand = string.Empty;
            NewProductCode = string.Empty;
            NewProductDescription = string.Empty;
            NewProductIsActive = true;
            NewProductSku = string.Empty;
            NewProductLabel = string.Empty;
            NewProductColor = string.Empty;
            NewProductSize = string.Empty;
            NewProductPrice = 0;
            NewProductQuantity = 0;
        }

        private async Task CreateStoreAsync()
        {
            if (!CanManageStores)
                return;

            if (string.IsNullOrWhiteSpace(NewStoreName))
            {
                _notify.Show("Store name is required", NotificationType.Warning);
                return;
            }

            var quota = await _tenantQuota.CanCreateStoreAsync(_auth.SelectedTenantId ?? 0);
            if (!quota.Allowed)
            {
                _notify.Show(quota.Message ?? "Store could not be created.", NotificationType.Warning);
                return;
            }

            var store = await _storeAdminRepo.CreateStoreAsync(new StoreRecord
            {
                TenantId = _auth.SelectedTenantId ?? 0,
                Name = NewStoreName.Trim(),
                IsActive = NewStoreIsActive,
            });

            Stores.Add(store);
            NewStoreName = string.Empty;
            NewStoreIsActive = true;
            IsCreateStorePopupOpen = false;
            if (NewUserStore == null && (!HasStoreScope || _auth.SelectedStoreId == store.Id))
                NewUserStore = store;
            this.RaisePropertyChanged(nameof(AvailableAssignableStores));
            this.RaisePropertyChanged(nameof(AvailableNewUserStores));
            this.RaisePropertyChanged(nameof(AvailableProductAssignmentStores));
            _notify.Show("Store created", NotificationType.Success);
        }

        private async Task ToggleStoreStatusAsync(StoreRecord store)
        {
            if (!CanManageStores || store == null)
                return;

            if (store.IsActive)
            {
                await DeactivateStoreAsync(store);
                return;
            }

            var updatedStore = await _storeAdminRepo.UpdateStoreAsync(new StoreRecord
            {
                Id = store.Id,
                TenantId = store.TenantId,
                Name = store.Name,
                Code = store.Code,
                IsActive = !store.IsActive,
            });

            _notify.Show(
                updatedStore.IsActive ? "Store marked as active" : "Store marked as inactive",
                NotificationType.Success);
            await LoadStoresAsync();
        }

        private async Task DeactivateStoreAsync(StoreRecord store)
        {
            if (!CanManageStores || store == null || !store.IsActive)
                return;

            var confirmed = await _confirmations.ConfirmActionAsync(
                "Deactivate store?",
                "This store will no longer be available for daily operations, but its history will be preserved.",
                "Deactivate",
                false);
            if (!confirmed)
                return;

            var updatedStore = await _storeAdminRepo.DeactivateStoreAsync(store);
            _notify.Show(
                updatedStore.IsActive ? "Store remains active" : "Store deactivated",
                NotificationType.Success);
            await LoadStoresAsync();
        }

        private async Task DeleteStoreAsync(StoreRecord store)
        {
            if (!CanManageStores || store == null)
                return;

            var confirmed = await _confirmations.ConfirmDeleteAsync(
                "Delete store permanently?",
                "This permanently removes the store if it has no dependent records. If the store has sales, stock movements, users, or historical records, deactivate it instead.");
            if (!confirmed)
                return;

            if (!await _storeAdminRepo.CanDeleteStoreAsync(store.Id))
            {
                _notify.Show("This store cannot be deleted because it has historical records. Deactivate it instead.", NotificationType.Warning);
                var deactivate = store.IsActive && await _confirmations.ConfirmActionAsync(
                    "Deactivate store?",
                    "This store will no longer be available for daily operations, but its history will be preserved.",
                    "Deactivate",
                    false);
                if (deactivate)
                {
                    await _storeAdminRepo.DeactivateStoreAsync(store);
                    _notify.Show("Store deactivated", NotificationType.Success);
                    await LoadStoresAsync();
                }
                return;
            }

            try
            {
                await _storeAdminRepo.DeleteStoreAsync(store.Id);
            }
            catch (Exception ex)
            {
                _notify.Show(ex.Message, NotificationType.Warning);
                return;
            }

            Stores.Remove(store);
            if (NewUserStore?.Id == store.Id)
                NewUserStore = null;
            this.RaisePropertyChanged(nameof(AvailableAssignableStores));
            this.RaisePropertyChanged(nameof(AvailableNewUserStores));
            this.RaisePropertyChanged(nameof(AvailableProductAssignmentStores));
            _notify.Show("Store deleted", NotificationType.Success);
        }

        private async Task LoadSelectedUserStoresAsync()
        {
            if (_isLoadingSelectedUserStores)
                return;

            _isLoadingSelectedUserStores = true;
            try
            {
                SelectedUserStores.Clear();
                SelectedAssignableStore = null;
                this.RaisePropertyChanged(nameof(SelectedUserStoreSummary));
                this.RaisePropertyChanged(nameof(AvailableAssignableStores));

                var user = SelectedUser;
                if (!CanManageSelectedUserStores || user == null)
                    return;

                var currentUserId = ResolveOnlineUserId(user);
                var assignments = await _storeAdminRepo.GetUserStoresAsync(currentUserId);

                if (SelectedUser == null || ResolveOnlineUserId(SelectedUser) != currentUserId)
                    return;

                var distinctAssignments = assignments
                    .Where(a => Stores.Any(s => s.Id == a.StoreId))
                    .GroupBy(a => new
                    {
                        a.StoreId,
                        Role = NormalizeStoreRole(a.Role),
                    })
                    .Select(group => group
                        .OrderByDescending(a => a.IsActive)
                        .ThenBy(a => a.StoreId)
                        .First())
                    .OrderBy(a => a.StoreName)
                    .ThenBy(a => a.Role)
                    .ToList();

                foreach (var assignment in distinctAssignments)
                    SelectedUserStores.Add(assignment);

                this.RaisePropertyChanged(nameof(SelectedUserStoreSummary));
                this.RaisePropertyChanged(nameof(AvailableAssignableStores));
            }
            finally
            {
                _isLoadingSelectedUserStores = false;
            }
        }

        private async Task LoadSelectedProductStoresAsync()
        {
            SelectedProductStores.Clear();
            SelectedProductAssignmentStore = null;
            this.RaisePropertyChanged(nameof(SelectedProductStoreSummary));
            this.RaisePropertyChanged(nameof(AvailableProductAssignmentStores));

            if (SelectedProduct == null || SelectedProduct.OnlineId <= 0)
                return;

            try
            {
                foreach (var assignment in await _productRepo.GetStoreAssignmentsAsync(SelectedProduct.OnlineId))
                    SelectedProductStores.Add(assignment);

                if (_auth.SelectedStoreId.HasValue)
                {
                    var currentStoreAssignment = SelectedProductStores.FirstOrDefault(a => a.StoreId == _auth.SelectedStoreId.Value && a.IsActive);
                    SelectedProduct.AssignmentId = currentStoreAssignment?.Id ?? 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Products] Failed to load store assignments for product {SelectedProduct.OnlineId}: {ex.Message}");
                _notify.Show("Could not load product store assignments.", NotificationType.Warning);
            }

            this.RaisePropertyChanged(nameof(SelectedProductStoreSummary));
            this.RaisePropertyChanged(nameof(AvailableProductAssignmentStores));
            this.RaisePropertyChanged(nameof(CanManageVariants));
            this.RaisePropertyChanged(nameof(HasCurrentStoreAssignment));
            this.RaisePropertyChanged(nameof(CurrentStoreAssignmentSummary));
        }

        private async Task LoadSelectedProductVariantsAsync()
        {
            SelectedProductVariants.Clear();
            ResetVariantEditor();
            this.RaisePropertyChanged(nameof(SelectedProductVariantSummary));
            this.RaisePropertyChanged(nameof(CanManageVariants));

            if (SelectedProduct == null || !_auth.SelectedStoreId.HasValue)
                return;

            foreach (var variant in await _productRepo.GetVariantsAsync(SelectedProduct.OnlineId, _auth.SelectedStoreId.Value))
                SelectedProductVariants.Add(variant);

            this.RaisePropertyChanged(nameof(SelectedProductVariantSummary));
            this.RaisePropertyChanged(nameof(CanManageVariants));
        }

        private async Task LoadStoreProductVariantsAsync()
        {
            StoreProductVariants.Clear();

            if (!IsStoreManager || !_auth.SelectedStoreId.HasValue)
            {
                this.RaisePropertyChanged(nameof(StoreProductVariantSummary));
                return;
            }

            var storeId = _auth.SelectedStoreId.Value;
            var variantTasks = Products
                .Where(product => product.OnlineId > 0)
                .Select(product => _productRepo.GetVariantsAsync(product.OnlineId, storeId))
                .ToList();

            var variantGroups = await Task.WhenAll(variantTasks);
            foreach (var variant in variantGroups.SelectMany(group => group))
                StoreProductVariants.Add(variant);

            this.RaisePropertyChanged(nameof(StoreProductVariantSummary));
        }

        private async Task OpenStoreProductVariantAsync(ProductVariantRecord variant)
        {
            var product = Products.FirstOrDefault(p => p.OnlineId == variant.ProductId || p.Id == variant.ProductId);
            if (product == null)
                return;

            SelectedProduct = product;
            SelectedEditProductCategory = Categories.FirstOrDefault(c => c.Id == product.CategoryId)
                ?? Categories.FirstOrDefault(c => string.Equals(c.Name, product.Category, StringComparison.OrdinalIgnoreCase));
            SelectedProductAssignmentPrice = product.Price;
            SelectedProductAssignmentQuantity = product.StockQuantity;

            await LoadSelectedProductStoresAsync();
            await LoadSelectedProductVariantsAsync();

            var refreshedVariant = SelectedProductVariants.FirstOrDefault(v => v.Id == variant.Id) ?? variant;
            IsEditingStoreVariant = true;
            EditProductVariant(refreshedVariant);
            ShowEditProductRequested?.Invoke(this, EventArgs.Empty);
        }

        private async Task AssignSelectedStoreAsync()
        {
            if (!CanManageSelectedUserStores || SelectedUser == null || SelectedAssignableStore == null)
                return;

            if (!HasAuthenticatedOnlineSession())
                return;

            await _storeAdminRepo.AssignUserToStoreAsync(
                ResolveOnlineUserId(SelectedUser),
                SelectedAssignableStore.TenantId != 0 ? SelectedAssignableStore.TenantId : _auth.SelectedTenantId ?? 0,
                SelectedAssignableStore.Id,
                SelectedStoreRole);

            await LoadSelectedUserStoresAsync();
            SelectedStoreRole = AvailableStoreRoles.FirstOrDefault() ?? AppRoles.Clerk;
            _notify.Show("Store assignment saved", NotificationType.Success);
        }

        private async Task RemoveUserStoreAsync(UserStoreAssignment assignment)
        {
            if (!CanManageAssignments)
                return;

            if (!HasAuthenticatedOnlineSession())
                return;

            var confirmed = await _confirmations.ConfirmDeleteAsync(
                "Remove store assignment?",
                $"Remove access to '{assignment.StoreName}' for this user? This action cannot be undone.");
            if (!confirmed)
                return;

            await _storeAdminRepo.RemoveUserFromStoreAsync(assignment.UserId, assignment.StoreId);
            SelectedUserStores.Remove(assignment);
            this.RaisePropertyChanged(nameof(SelectedUserStoreSummary));
            _notify.Show("Store assignment removed", NotificationType.Success);
        }

        private bool HasAuthenticatedOnlineSession()
        {
            if (!_net.IsOnline())
            {
                _notify.Show("Internet required", NotificationType.Warning);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(_tokens.Token))
                return true;

            _notify.Show("Please sign in online again to manage users and store assignments.", NotificationType.Warning);
            return false;
        }

        private async Task SaveProductStoreAssignmentAsync()
        {
            if (SelectedProduct == null || SelectedProduct.OnlineId <= 0)
                return;

            if (IsStoreManager && _auth.SelectedStoreId.HasValue)
                SelectedProductAssignmentStore = Stores.FirstOrDefault(store => store.Id == _auth.SelectedStoreId.Value);

            if (SelectedProductAssignmentStore == null)
                return;

            try
            {
                await _productRepo.SaveStoreAssignmentAsync(
                    SelectedProduct.OnlineId,
                    new ProductStoreAssignment
                    {
                        ProductId = SelectedProduct.OnlineId,
                        TenantId = SelectedProductAssignmentStore.TenantId != 0
                            ? SelectedProductAssignmentStore.TenantId
                            : _auth.SelectedTenantId ?? 0,
                        StoreId = SelectedProductAssignmentStore.Id,
                        IsActive = true,
                    });

                await LoadSelectedProductStoresAsync();
                await LoadSelectedProductVariantsAsync();
                await LoadCatalogAsync();
                _notify.Show("Product assigned to store", NotificationType.Success);
            }
            catch (Exception ex)
            {
                var message = ex.Message.Contains("does not support separate product-store assignments", StringComparison.OrdinalIgnoreCase)
                    ? "This database links products to stores through store variants. Create a variant for this store instead of a separate assignment."
                    : ex.Message;
                _notify.Show(message, NotificationType.Warning);
            }
        }

        private async Task RemoveProductStoreAssignmentAsync(ProductStoreAssignment assignment)
        {
            if (SelectedProduct == null || SelectedProduct.OnlineId <= 0)
                return;

            var confirmed = await _confirmations.ConfirmDeleteAsync(
                "Remove product assignment?",
                $"Remove this product from '{assignment.StoreName}'? Store variants and daily operations may be affected.");
            if (!confirmed)
                return;

            await _productRepo.RemoveStoreAssignmentAsync(SelectedProduct.OnlineId, assignment.StoreId);
            SelectedProductStores.Remove(assignment);
            this.RaisePropertyChanged(nameof(SelectedProductStoreSummary));
            this.RaisePropertyChanged(nameof(AvailableProductAssignmentStores));
            this.RaisePropertyChanged(nameof(HasCurrentStoreAssignment));
            this.RaisePropertyChanged(nameof(CurrentStoreAssignmentSummary));
            await LoadSelectedProductVariantsAsync();
            await LoadCatalogAsync();
            _notify.Show("Product store assignment removed", NotificationType.Success);
        }

        private void EditProductVariant(ProductVariantRecord variant)
        {
            SelectedProductVariant = variant;
            VariantLabel = variant.Label;
            VariantSku = variant.SKU;
            VariantBarcode = variant.Barcode;
            VariantPrice = variant.Price;
            VariantCostPrice = variant.CostPrice ?? 0;
            VariantStockQuantity = variant.StockQuantity;
            VariantIsActive = variant.IsActive;
            BarcodePrintQuantity = Math.Max(variant.StockQuantity, 1);
        }

        private async Task SaveProductVariantAsync()
        {
            if (!CanManageVariants || SelectedProduct == null || !_auth.SelectedStoreId.HasValue)
                return;

            if (SelectedProduct.OnlineId <= 0)
            {
                _notify.Show("This product is not synced to the online catalog yet. Sign in online and sync before creating store variants.", NotificationType.Warning);
                return;
            }

            if (!TryParseDecimal(VariantPriceText, out var parsedVariantPrice) || parsedVariantPrice <= 0)
            {
                _notify.Show("Variant price must be greater than zero", NotificationType.Warning);
                return;
            }

            if (!TryParseInt(VariantStockQuantityText, out var parsedVariantStockQuantity))
            {
                _notify.Show("Variant stock must be a whole number", NotificationType.Warning);
                return;
            }

            if (parsedVariantStockQuantity < 0)
            {
                _notify.Show("Variant stock cannot be negative", NotificationType.Warning);
                return;
            }

            var assignmentId = SelectedProductStores
                .FirstOrDefault(a => a.StoreId == _auth.SelectedStoreId.Value && a.IsActive)?.Id
                ?? SelectedProduct.AssignmentId;

            var saved = await _productRepo.SaveVariantAsync(SelectedProduct.OnlineId, new ProductVariantRecord
            {
                Id = SelectedProductVariant?.Id ?? 0,
                ProductId = SelectedProduct.OnlineId,
                AssignmentId = assignmentId,
                TenantId = _auth.SelectedTenantId ?? 0,
                StoreId = _auth.SelectedStoreId.Value,
                Label = VariantLabel.Trim(),
                SKU = VariantSku.Trim(),
                Barcode = VariantBarcode.Trim(),
                Price = parsedVariantPrice,
                CostPrice = VariantCostPrice <= 0 ? null : VariantCostPrice,
                StockQuantity = parsedVariantStockQuantity,
                IsActive = VariantIsActive,
            });

            if (saved != null)
            {
                ResetVariantEditor();
                await LoadSelectedProductVariantsAsync();
                await LoadCatalogAsync();
                _notify.Show("Store variant saved", NotificationType.Success);
            }
        }

        private async Task DeleteProductVariantAsync(ProductVariantRecord variant)
        {
            if (!CanManageVariants)
                return;

            var confirmed = await _confirmations.ConfirmDeleteAsync(
                "Delete product variant?",
                $"Delete variant '{variant.DisplayName}'? Use Deactivate instead if you only want to hide it from daily operations.");
            if (!confirmed)
                return;

            await _productRepo.DeleteVariantAsync(variant.Id);
            await LoadSelectedProductVariantsAsync();
            await LoadCatalogAsync();
            _notify.Show("Store variant deactivated", NotificationType.Success);
        }

        private async Task GenerateVariantBarcodeAsync()
        {
            var feature = await _featureGate.EnsureFeatureAsync(LicenseFeature.BarcodePrinting, _auth.SelectedTenantId);
            if (!feature.Allowed)
            {
                _notify.Show(feature.Message ?? "Barcode printing is not included in the current license.", NotificationType.Warning);
                return;
            }

            if (SelectedProductVariant == null)
            {
                _notify.Show("Select a saved variant first.", NotificationType.Warning);
                return;
            }

            var updated = await _productRepo.GenerateVariantBarcodeAsync(SelectedProductVariant.Id);
            if (updated != null)
            {
                EditProductVariant(updated);
                await LoadSelectedProductVariantsAsync();
                _notify.Show("Barcode generated", NotificationType.Success);
            }
        }

        private async Task PrintVariantBarcodeAsync()
        {
            var feature = await _featureGate.EnsureFeatureAsync(LicenseFeature.BarcodePrinting, _auth.SelectedTenantId);
            if (!feature.Allowed)
            {
                _notify.Show(feature.Message ?? "Barcode printing is not included in the current license.", NotificationType.Warning);
                return;
            }

            if (SelectedProductVariant == null)
            {
                _notify.Show("Select a saved variant first.", NotificationType.Warning);
                return;
            }

            OpenBarcodePrintPopup(SelectedProductVariant, SelectedProduct?.Name ?? SelectedProductVariant.Name);
            await Task.CompletedTask;
        }

        private async Task PrintStoreProductVariantAsync(ProductVariantRecord variant)
        {
            OpenBarcodePrintPopup(variant, variant.Name);
            await Task.CompletedTask;
        }

        private void OpenBarcodePrintPopup(ProductVariantRecord variant, string productName)
        {
            SelectedProductVariant = variant;

            if (string.IsNullOrWhiteSpace(variant.Barcode))
            {
                _notify.Show("Generate or assign a barcode before printing.", NotificationType.Warning);
                return;
            }

            BarcodePrintVariant = variant;
            BarcodePrintProductName = productName;
            BarcodePrintQuantity = Math.Max(variant.StockQuantity, 1);
            BarcodePrintQuantityText = BarcodePrintQuantity.ToString(CultureInfo.CurrentCulture);
            IsBarcodePrintPopupOpen = true;
        }

        private async Task ConfirmBarcodePrintAsync()
        {
            if (BarcodePrintVariant == null)
                return;

            if (!TryParseInt(BarcodePrintQuantityText, out var quantity) || quantity < 1)
            {
                _notify.Show("Enter a valid number of labels to print.", NotificationType.Warning);
                return;
            }

            BarcodePrintQuantity = quantity;

            var variant = BarcodePrintVariant;
            var productName = BarcodePrintProductName;
            CloseBarcodePrintPopup();

            await PrintVariantBarcodeAsync(variant, productName, quantity);
        }

        private async Task PrintVariantBarcodeAsync(ProductVariantRecord variant, string productName, int quantity)
        {
            SelectedProductVariant = variant;

            var image = await _productRepo.GetVariantBarcodeImageAsync(variant.Id);
            if (image == null || image.Length == 0)
            {
                _notify.Show("Could not load barcode image.", NotificationType.Warning);
                return;
            }

            var folder = Path.Combine(Path.GetTempPath(), "Xavissa", "Barcodes");
            Directory.CreateDirectory(folder);
            var imagePath = Path.Combine(folder, $"variant-{variant.Id}.png");
            await File.WriteAllBytesAsync(imagePath, image);

            _printer.PrintBarcodeLabel(
                productName,
                variant.Label,
                variant.SKU,
                variant.Price,
                variant.Barcode,
                imagePath,
                quantity);
            _notify.Show(quantity == 1 ? "Barcode label sent to printer" : $"{quantity} barcode labels sent to printer", NotificationType.Success);
        }

        private void CloseBarcodePrintPopup()
        {
            IsBarcodePrintPopupOpen = false;
            BarcodePrintVariant = null;
            BarcodePrintProductName = string.Empty;
            BarcodePrintQuantity = 1;
            BarcodePrintQuantityText = "1";
        }

        private void ResetVariantEditor()
        {
            SelectedProductVariant = null;
            VariantLabel = string.Empty;
            VariantSku = string.Empty;
            VariantBarcode = string.Empty;
            VariantPrice = 0;
            VariantCostPrice = 0;
            VariantStockQuantity = 0;
            VariantIsActive = true;
            CloseBarcodePrintPopup();
        }

        private static bool TryParseDecimal(string? value, out decimal parsed)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                parsed = 0;
                return false;
            }

            return decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out parsed)
                || decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed);
        }

        private static bool TryParseInt(string? value, out int parsed)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                parsed = 0;
                return true;
            }

            return int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out parsed)
                || int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
        }

        private void PrefillVariantEditorFromSelectedProduct()
        {
            if (!IsStoreManager || SelectedProduct == null)
                return;

            ResetVariantEditor();
            VariantLabel = string.Empty;
            VariantSku = string.Empty;
            VariantBarcode = string.Empty;
            VariantPrice = SelectedProduct.Price > 0 ? SelectedProduct.Price : VariantPrice;
            VariantStockQuantity = SelectedProduct.StockQuantity > 0 ? SelectedProduct.StockQuantity : VariantStockQuantity;
            VariantIsActive = true;
        }

        private void ResetNewUserForm()
        {
            NewUsername = string.Empty;
            NewEmail = string.Empty;
            NewPassword = string.Empty;
            NewUserRole = AvailableUserRoles.FirstOrDefault() ?? AppRoles.Clerk;
            NewUserStore = Stores.FirstOrDefault(store => !HasStoreScope || store.Id == _auth.SelectedStoreId);
        }

        private bool MatchesTeamUserFilter(User user)
        {
            if (user == null)
                return false;

            var matchesFilter = SelectedTeamUserFilter switch
            {
                TeamUserFilter.Active => user.IsActive,
                TeamUserFilter.Inactive => !user.IsActive,
                _ => true,
            };

            if (!matchesFilter)
                return false;

            if (string.IsNullOrWhiteSpace(TeamUserSearchText))
                return true;

            var search = TeamUserSearchText.Trim();
            return user.Username.Contains(search, StringComparison.OrdinalIgnoreCase)
                || user.EmailDisplay.Contains(search, StringComparison.OrdinalIgnoreCase)
                || user.EffectiveRole.Contains(search, StringComparison.OrdinalIgnoreCase)
                || user.AssignedStoreSummary.Contains(search, StringComparison.OrdinalIgnoreCase);
        }

        private void OnUserPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(User.IsSelected))
                RefreshTeamUserListState();
        }

        private void RefreshTeamUserListState()
        {
            this.RaisePropertyChanged(nameof(VisibleUsers));
            this.RaisePropertyChanged(nameof(SelectedUsers));
            this.RaisePropertyChanged(nameof(TeamSelectionSummary));
            this.RaisePropertyChanged(nameof(AllVisibleUsersSelected));
            this.RaisePropertyChanged(nameof(HasSelectedUsers));
            this.RaisePropertyChanged(nameof(HasSelectedActiveUsers));
            this.RaisePropertyChanged(nameof(HasSelectedInactiveUsers));
            RaiseEmptyStateProperties();
        }

        private void RaiseEmptyStateProperties()
        {
            this.RaisePropertyChanged(nameof(ShowUsersEmptyState));
            this.RaisePropertyChanged(nameof(ShowStoresEmptyState));
            this.RaisePropertyChanged(nameof(ShowCategoriesEmptyState));
            this.RaisePropertyChanged(nameof(ShowCatalogEmptyState));
            this.RaisePropertyChanged(nameof(ShowVariantsEmptyState));
            this.RaisePropertyChanged(nameof(EmptyUsersText));
            this.RaisePropertyChanged(nameof(EmptyStoresText));
            this.RaisePropertyChanged(nameof(EmptyCategoriesText));
            this.RaisePropertyChanged(nameof(EmptyCatalogText));
            this.RaisePropertyChanged(nameof(EmptyVariantsText));
        }

        private static int ResolveOnlineUserId(User user) => user.EffectiveOnlineUserId;

        private static string NormalizeStoreRole(string? role)
        {
            if (string.IsNullOrWhiteSpace(role))
                return AppRoles.Clerk;

            if (AppRoles.IsStoreManager(role))
                return AppRoles.StoreManager;

            if (AppRoles.IsClerkLike(role))
                return AppRoles.Clerk;

            return AppRoles.Clerk;
        }
    }
}
