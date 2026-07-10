using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Xavissa.Frontend.Models
{
    public class HistorySaleItem : INotifyPropertyChanged
    {
        private int _id;
        private string _date = "";
        private string _item = "";
        private string _price = "";
        private int _quantity;
        private string _subtotal = "";

        [JsonPropertyName("id")]
        public int Id
        {
            get => _id;
            set
            {
                _id = value;
                OnPropertyChanged(nameof(Id));
            }
        }

        [JsonPropertyName("date")]
        public string Date
        {
            get => _date;
            set
            {
                _date = value;
                OnPropertyChanged(nameof(Date));
            }
        }

        // If your API returns "productName", this will automatically map
        [JsonPropertyName("productName")]
        public string Item
        {
            get => _item;
            set
            {
                _item = value;
                OnPropertyChanged(nameof(Item));
            }
        }

        // If backend returns numeric value, it's fine — it will convert to string
        [JsonPropertyName("price")]
        public string Price
        {
            get => _price;
            set
            {
                _price = value;
                OnPropertyChanged(nameof(Price));
            }
        }

        [JsonPropertyName("quantity")]
        public int Quantity
        {
            get => _quantity;
            set
            {
                _quantity = value;
                OnPropertyChanged(nameof(Quantity));
            }
        }

        // If backend returns "total" instead of "subtotal", this will work
        [JsonPropertyName("total")]
        public string Subtotal
        {
            get => _subtotal;
            set
            {
                _subtotal = value;
                OnPropertyChanged(nameof(Subtotal));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public HistorySaleItem() { } // Needed for deserialization

        public HistorySaleItem(
            int id,
            string date,
            string item,
            string price,
            int quantity,
            string subtotal
        )
        {
            _id = id;
            _date = date;
            _item = item;
            _price = price;
            _quantity = quantity;
            _subtotal = subtotal;
        }
    }
}
