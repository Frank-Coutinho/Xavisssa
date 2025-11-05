using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using Xavissa.Frontend.Helpers;
using Xavissa.Frontend.Models;
using Xavissa.Frontend.Services;

namespace Xavissa.Frontend.ViewModels
{
    public class SaleItem : INotifyPropertyChanged
    {
        public Product Product { get; set; } = new();
        private int _quantity = 1;

        public int Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity == value)
                    return;
                _quantity = value;
                OnPropertyChanged(nameof(Quantity));
                OnPropertyChanged(nameof(Total));
            }
        }

        public decimal Total => Quantity * Product.Price;

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class HomeViewModel : ViewModelBase
    {
        private decimal _totalSales;
        public decimal TotalSales
        {
            get => _totalSales;
            set => this.RaiseAndSetIfChanged(ref _totalSales, value);
        }

        private Product? _topProduct;
        public Product? TopProduct
        {
            get => _topProduct;
            set => this.RaiseAndSetIfChanged(ref _topProduct, value);
        }

        private int _recentSalesCount;
        public int RecentSalesCount
        {
            get => _recentSalesCount;
            set => this.RaiseAndSetIfChanged(ref _recentSalesCount, value);
        }

        public ObservableCollection<SaleItem> RecentSales { get; } = new();

        private readonly HttpClient _httpClient;

        public ObservableCollection<Product> Products { get; } = new();
        public ObservableCollection<SaleItem> CartItems { get; } = new();

        private decimal _discount;
        public decimal Discount
        {
            get => _discount;
            set => this.RaiseAndSetIfChanged(ref _discount, value);
        }

        private decimal _amountPaid;
        public decimal AmountPaid
        {
            get => _amountPaid;
            set => this.RaiseAndSetIfChanged(ref _amountPaid, value);
        }

        public decimal Subtotal => CartItems.Sum(i => i.Total);
        public decimal Total => CartItems.Sum(i => i.Total);
        public decimal FinalTotal => Math.Max(Total - Discount, 0);
        public decimal ChangeAmount => Math.Max(AmountPaid - FinalTotal, 0);

        public ObservableCollection<string> PaymentMethods { get; } =
            new() { "Cash", "Card", "Mpesa", "Other" };

        private string _selectedPaymentMethod = "Cash";
        public string SelectedPaymentMethod
        {
            get => _selectedPaymentMethod;
            set => this.RaiseAndSetIfChanged(ref _selectedPaymentMethod, value);
        }

        public ReactiveCommand<Product, Unit> AddToCartCommand { get; }
        public ReactiveCommand<SaleItem, Unit> RemoveFromCartCommand { get; }
        public ReactiveCommand<SaleItem, Unit> IncreaseQuantityCommand { get; }
        public ReactiveCommand<SaleItem, Unit> DecreaseQuantityCommand { get; }
        public ReactiveCommand<Unit, Unit> FinalizarSaleCommand { get; }
        public ReactiveCommand<Unit, Unit> ClearCartCommand { get; }
        public string CurrentDateTime => DateTime.Now.ToString("dd MMM yyyy, HH:mm");

        public HomeViewModel()
        {
            _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5087") };
            if (AuthService.IsLoggedIn)
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Bearer",
                        AuthService.Token
                    );
            }

            AddToCartCommand = ReactiveCommand.Create<Product>(product =>
            {
                try
                {
                    AddToCart(product);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error adding to cart: {ex}");
                }
            });

            ClearCartCommand = ReactiveCommand.Create(ClearCart);
            RemoveFromCartCommand = ReactiveCommand.Create<SaleItem>(RemoveFromCart);
            IncreaseQuantityCommand = ReactiveCommand.Create<SaleItem>(IncreaseQuantity);
            DecreaseQuantityCommand = ReactiveCommand.Create<SaleItem>(DecreaseQuantity);
            FinalizarSaleCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                try
                {
                    await FinalizarSaleAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error finalizing sale: {ex}");
                }
            });

            CartItems.CollectionChanged += CartItems_CollectionChanged;

            _ = LoadProductsAsync();
        }

        private async Task LoadProductsAsync()
        {
            try
            {
                var products = await _httpClient.GetFromJsonAsync<Product[]>("/api/Product");
                Products.Clear();
                if (products != null)
                    foreach (var p in products)
                        Products.Add(p);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading products: " + ex.Message);
            }
        }

        private void AddToCart(Product product)
        {
            var existing = CartItems.FirstOrDefault(i => i.Product.Id == product.Id);
            if (existing != null)
                existing.Quantity++;
            else
            {
                var item = new SaleItem { Product = product };
                CartItems.Add(item);
                AttachItem(item);
            }
            RaiseTotals();
        }

        private void RemoveFromCart(SaleItem item)
        {
            DetachItem(item);
            CartItems.Remove(item);
            RaiseTotals();
        }

        private void IncreaseQuantity(SaleItem item)
        {
            if (item == null)
                return;
            item.Quantity++;
            RaiseTotals();
        }

        private void DecreaseQuantity(SaleItem item)
        {
            if (item == null)
                return;
            if (item.Quantity > 1)
                item.Quantity--;
            else
                RemoveFromCart(item);
            RaiseTotals();
        }

        private void AttachItem(SaleItem item)
        {
            if (item is INotifyPropertyChanged npc)
                npc.PropertyChanged += SaleItem_PropertyChanged;
        }

        private void DetachItem(SaleItem item)
        {
            if (item is INotifyPropertyChanged npc)
                npc.PropertyChanged -= SaleItem_PropertyChanged;
        }

        private void CartItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (var ni in e.NewItems.OfType<SaleItem>())
                    AttachItem(ni);
            if (e.OldItems != null)
                foreach (var oi in e.OldItems.OfType<SaleItem>())
                    DetachItem(oi);

            RaiseTotals();
        }

        private void SaleItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(SaleItem.Quantity) or nameof(SaleItem.Total))
                RaiseTotals();
        }

        private void ClearCart()
        {
            foreach (var item in CartItems.ToList())
                DetachItem(item);

            CartItems.Clear();
            Discount = 0;
            AmountPaid = 0;
            RaiseTotals();
        }

        private void RaiseTotals()
        {
            this.RaisePropertyChanged(nameof(Total));
            this.RaisePropertyChanged(nameof(FinalTotal));
            this.RaisePropertyChanged(nameof(ChangeAmount));
        }

        private int MapPaymentMethodToInt(string method) =>
            method switch
            {
                "Cash" => 0,
                "Card" => 1,
                "Mpesa" => 2,
                _ => 0,
            };

        private async Task FinalizarSaleAsync()
        {
            try
            {
                int paymentMethodInt = MapPaymentMethodToInt(SelectedPaymentMethod);

                var saleData = new
                {
                    SaleItems = CartItems
                        .Select(i => new
                        {
                            ProductId = i.Product.Id,
                            Quantity = i.Quantity,
                            PaymentMethod = paymentMethodInt,
                            Discount,
                            AmountPaid,
                            Change = ChangeAmount,
                        })
                        .ToList(),
                };

                var response = await _httpClient.PostAsJsonAsync("/api/Sales", saleData);
                response.EnsureSuccessStatusCode();

                // Build and print/save receipt
                var printerService = new PrinterService();
                var receiptBuilder = new ReceiptBuilder(this, printerService);
                receiptBuilder.PrintReceipt();

                // Clear cart
                foreach (var item in CartItems.ToList())
                    DetachItem(item);

                CartItems.Clear();
                Discount = 0;
                AmountPaid = 0;
                RaiseTotals();
                await LoadProductsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error creating sale: " + ex.Message);
            }
        }
    }
}
