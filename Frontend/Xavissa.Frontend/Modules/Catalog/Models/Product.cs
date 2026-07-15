using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;

namespace Xavissa.Frontend.Models
{
    public class Product : INotifyPropertyChanged
    {
        private int _id;
        private int _onlineId;
        private Guid _syncId = Guid.NewGuid();
        private string? _sourceDeviceId;
        private DateTimeOffset? _clientCreatedAt;
        private DateTimeOffset? _clientUpdatedAt;
        private DateTimeOffset? _lastSyncedAt;
        private int _tenantId;
        private int _variantId;
        private int _assignmentId;
        private int _storeId;
        private int? _categoryId;
        private string _code = string.Empty;
        private string _barcode = string.Empty;
        private string _name = string.Empty;
        private string _color = string.Empty;
        private string _size = string.Empty;
        private string _sku = string.Empty;
        private string _label = string.Empty;
        private string _attributesJson = string.Empty;
        private string _imageUrl = string.Empty;
        private string? _description;
        private string _category = string.Empty;
        private string _brand = string.Empty;
        private decimal _price;
        private int _stockQuantity;
        private bool _isActive = true;
        private int _variantCount;
        private DateTime _createdAt = DateTime.UtcNow;
        private DateTime _updatedAt = DateTime.UtcNow;
        private int _lowStockThreshold = 5;

        public int Id { get => _id; set { if (_id != value) { _id = value; OnPropertyChanged(nameof(Id)); } } }
        public int OnlineId { get => _onlineId; set { if (_onlineId != value) { _onlineId = value; OnPropertyChanged(nameof(OnlineId)); } } }
        public Guid SyncId { get => _syncId; set { if (_syncId != value) { _syncId = value; OnPropertyChanged(nameof(SyncId)); } } }
        public string? SourceDeviceId { get => _sourceDeviceId; set { if (_sourceDeviceId != value) { _sourceDeviceId = value; OnPropertyChanged(nameof(SourceDeviceId)); } } }
        public DateTimeOffset? ClientCreatedAt { get => _clientCreatedAt; set { if (_clientCreatedAt != value) { _clientCreatedAt = value; OnPropertyChanged(nameof(ClientCreatedAt)); } } }
        public DateTimeOffset? ClientUpdatedAt { get => _clientUpdatedAt; set { if (_clientUpdatedAt != value) { _clientUpdatedAt = value; OnPropertyChanged(nameof(ClientUpdatedAt)); } } }
        public DateTimeOffset? LastSyncedAt { get => _lastSyncedAt; set { if (_lastSyncedAt != value) { _lastSyncedAt = value; OnPropertyChanged(nameof(LastSyncedAt)); } } }
        public int TenantId { get => _tenantId; set { if (_tenantId != value) { _tenantId = value; OnPropertyChanged(nameof(TenantId)); } } }
        public int VariantId { get => _variantId; set { if (_variantId != value) { _variantId = value; OnPropertyChanged(nameof(VariantId)); } } }
        public int AssignmentId { get => _assignmentId; set { if (_assignmentId != value) { _assignmentId = value; OnPropertyChanged(nameof(AssignmentId)); } } }
        public int StoreId { get => _storeId; set { if (_storeId != value) { _storeId = value; OnPropertyChanged(nameof(StoreId)); } } }
        public int? CategoryId { get => _categoryId; set { if (_categoryId != value) { _categoryId = value; OnPropertyChanged(nameof(CategoryId)); } } }
        public string Code { get => _code; set { if (_code != value) { _code = value; OnPropertyChanged(nameof(Code)); } } }
        public string Barcode { get => _barcode; set { if (_barcode != value) { _barcode = value; OnPropertyChanged(nameof(Barcode)); } } }
        public string Name { get => _name; set { if (_name != value) { _name = value; OnPropertyChanged(nameof(Name)); } } }
        public string Color { get => _color; set { if (_color != value) { _color = value; OnPropertyChanged(nameof(Color)); } } }
        public string Size { get => _size; set { if (_size != value) { _size = value; OnPropertyChanged(nameof(Size)); } } }
        public string SKU { get => _sku; set { if (_sku != value) { _sku = value; OnPropertyChanged(nameof(SKU)); } } }
        public string Label { get => _label; set { if (_label != value) { _label = value; OnPropertyChanged(nameof(Label)); } } }
        public string AttributesJson { get => _attributesJson; set { if (_attributesJson != value) { _attributesJson = value; OnPropertyChanged(nameof(AttributesJson)); } } }
        public string ImageUrl { get => _imageUrl; set { if (_imageUrl != value) { _imageUrl = value; OnPropertyChanged(nameof(ImageUrl)); } } }
        public string? Description { get => _description; set { if (_description != value) { _description = value; OnPropertyChanged(nameof(Description)); } } }
        public string Category { get => _category; set { if (_category != value) { _category = value; OnPropertyChanged(nameof(Category)); } } }
        public string Brand { get => _brand; set { if (_brand != value) { _brand = value; OnPropertyChanged(nameof(Brand)); } } }
        public decimal Price { get => _price; set { if (_price != value) { _price = value; OnPropertyChanged(nameof(Price)); } } }
        public int StockQuantity { get => _stockQuantity; set { if (_stockQuantity != value) { _stockQuantity = value; OnPropertyChanged(nameof(StockQuantity)); OnPropertyChanged(nameof(IsLowStock)); OnPropertyChanged(nameof(LowStockWarningText)); } } }
        public bool IsActive { get => _isActive; set { if (_isActive != value) { _isActive = value; OnPropertyChanged(nameof(IsActive)); } } }
        public int VariantCount { get => _variantCount; set { if (_variantCount != value) { _variantCount = value; OnPropertyChanged(nameof(VariantCount)); } } }
        public DateTime CreatedAt { get => _createdAt; set { if (_createdAt != value) { _createdAt = value; OnPropertyChanged(nameof(CreatedAt)); } } }
        public DateTime UpdatedAt { get => _updatedAt; set { if (_updatedAt != value) { _updatedAt = value; OnPropertyChanged(nameof(UpdatedAt)); } } }

        [NotMapped]
        public int LowStockThreshold
        {
            get => _lowStockThreshold;
            set
            {
                var normalized = Math.Max(0, value);
                if (_lowStockThreshold == normalized)
                    return;
                _lowStockThreshold = normalized;
                OnPropertyChanged(nameof(LowStockThreshold));
                OnPropertyChanged(nameof(IsLowStock));
                OnPropertyChanged(nameof(LowStockWarningText));
            }
        }

        [NotMapped]
        public bool IsLowStock => StockQuantity <= LowStockThreshold;

        [NotMapped]
        public string LowStockWarningText => StockQuantity <= 0
            ? "Out of stock"
            : IsLowStock
                ? $"Low stock - only {StockQuantity} left"
                : string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
