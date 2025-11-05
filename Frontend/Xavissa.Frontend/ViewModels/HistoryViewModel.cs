using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;

namespace Xavissa.Frontend.ViewModels
{
    public class HistoryViewModel : ViewModelBase
    {
        private string _currentDateTime;
        private decimal _total;
        private bool _isLoading;
        private string _errorMessage;
        private DateTime? _filterDate;

        public string CurrentDateTime
        {
            get => _currentDateTime;
            set => this.RaiseAndSetIfChanged(ref _currentDateTime, value);
        }

        public ObservableCollection<HistorySaleItem> Sales { get; } = new();
        public ObservableCollection<HistorySaleItem> FilteredSales { get; } = new();

        public decimal Total
        {
            get => _total;
            set => this.RaiseAndSetIfChanged(ref _total, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => this.RaiseAndSetIfChanged(ref _isLoading, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
        }

        public DateTime? FilterDate
        {
            get => _filterDate;
            set => this.RaiseAndSetIfChanged(ref _filterDate, value);
        }

        public ReactiveCommand<Unit, Unit> ApplyDateFilterCommand { get; }

        public HistoryViewModel()
        {
            CurrentDateTime = DateTime.Now.ToString("dd MMM yyyy - HH:mm:ss");
            ApplyDateFilterCommand = ReactiveCommand.Create(ApplyDateFilter);
            _ = LoadSalesAsync();
        }

        private async Task LoadSalesAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                using var client = new HttpClient { BaseAddress = new Uri("http://localhost:5087") };
                var response = await client.GetAsync("api/sales");
                response.EnsureSuccessStatusCode();

                var sales = await response.Content.ReadFromJsonAsync<HistorySaleItem[]>();
                Sales.Clear();
                FilteredSales.Clear();

                if (sales != null)
                {
                    foreach (var sale in sales)
                    {
                        Sales.Add(sale);
                        FilteredSales.Add(sale);
                    }

                    CalculateTotal(FilteredSales);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load sales: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ApplyDateFilter()
        {
            if (FilterDate == null)
            {
                FilteredSales.Clear();
                foreach (var sale in Sales)
                    FilteredSales.Add(sale);
            }
            else
            {
                var selected = FilterDate.Value.Date.ToString("yyyy-MM-dd");
                var filtered = Sales.Where(s => s.Date.Contains(selected)).ToList();

                FilteredSales.Clear();
                foreach (var sale in filtered)
                    FilteredSales.Add(sale);
            }

            CalculateTotal(FilteredSales);
        }

        private void CalculateTotal(ObservableCollection<HistorySaleItem> list)
        {
            decimal total = 0;
            foreach (var s in list)
            {
                if (decimal.TryParse(s.Subtotal?.Replace("MZN", "").Trim(), out var value))
                    total += value;
            }
            Total = total;
        }
    }

    public class HistorySaleItem
    {
        public int Id { get; set; }
        public string Date { get; set; }
        public string Item { get; set; }
        public string Price { get; set; }
        public int Quantity { get; set; }
        public string Subtotal { get; set; }
    }
}
