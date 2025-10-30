using System;
using System.Collections.Generic;
using System.Linq;
using Xavissa.Database;
using Xavissa.Database.Models;
using ZXing;
using ZXing.Common;
using System.Drawing;
using System.Drawing.Imaging; 

namespace Xavissa.Backend.Services
{
    public class ProductService(XavissaDbContext db)
    {
        private readonly XavissaDbContext _db = db;

        // Get all products
        public List<Product> GetAllProducts()
        {
            return _db.Products.ToList();
        }

        // Get products by category (based on Description or Name as fallback)
        public List<Product> GetProductsByCategory(string category)
        {
            return _db.Products
                .Where(p => p.Description.Contains(category, StringComparison.OrdinalIgnoreCase) 
                         || p.Name.Contains(category, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Get product by ID
        public Product? GetProductById(int id)
        {
            return _db.Products.FirstOrDefault(p => p.Id == id);
        }

        // Get all distinct categories from products
        public List<string> GetAllCategories()
        {
            return _db.Products
                .Select(p => p.Description) // Assuming Description represents category
                .Distinct()
                .OrderBy(c => c)
                .ToList();
        }

        private string GenerateBarcode(string prefix = "560")
            {
                var random = new Random();
    
                // Generate 9 random digits (12 total = 3 prefix + 9)
                var code = prefix;
                for (int i = 0; i < 9; i++)
                    code += random.Next(0, 10).ToString();

                // Calculate check digit
                int sum = 0;
                for (int i = 0; i < code.Length; i++)
                {
                    int digit = int.Parse(code[i].ToString());
                    sum += (i % 2 == 0) ? digit : digit * 3;
                }

                int checkDigit = (10 - (sum % 10)) % 10;

                return code + checkDigit.ToString();
            }


        // Add a new product
        public void AddProduct(Product product)
        {
            if (string.IsNullOrWhiteSpace(product.Name))
                throw new ArgumentException("Product name is required.");

            if (product.Price <= 0)
                throw new ArgumentException("Price must be greater than zero.");

            if (string.IsNullOrWhiteSpace(product.Barcode))
            {
                product.Barcode = GenerateBarcode();

                // Ensure uniqueness
                while (_db.Products.Any(p => p.Barcode == product.Barcode))
                {
                    product.Barcode = GenerateBarcode();
                }
            }

            // Validate category
            if (!Enum.IsDefined(typeof(ProductCategory), product.Category))
                throw new ArgumentException("Invalid product category.");

            product.LastModified = DateTime.UtcNow;

            _db.Products.Add(product);
            _db.SaveChanges();
        }

        public BitmapData LockBits(
            Rectangle rect,
            ImageLockMode flags,
            PixelFormat format
        )
        {
            throw new NotImplementedException();
        }
        

        public byte[] GenerateBarcodeImage(string barcode)
        {
            var writer = new BarcodeWriterPixelData
            {
                Format = BarcodeFormat.CODE_128,
                Options = new EncodingOptions
                {
                    Height = 100,
                    Width = 300,
                    Margin = 2
                }
            };

            var pixelData = writer.Write(barcode);

            using var bitmap = new Bitmap(pixelData.Width, pixelData.Height, PixelFormat.Format32bppRgb);

            // Lock the bitmap
            var bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, pixelData.Width, pixelData.Height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppRgb);

            System.Runtime.InteropServices.Marshal.Copy(pixelData.Pixels, 0, bitmapData.Scan0, pixelData.Pixels.Length);

            bitmap.UnlockBits(bitmapData);

            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);

            return stream.ToArray();
        }


        

        // Update an existing product
        public void UpdateProduct(Product product)
        {
            if (product.Id <= 0)
                throw new ArgumentException("Invalid product ID.");

            var existingProduct = _db.Products.FirstOrDefault(p => p.Id == product.Id);
            if (existingProduct == null)
                throw new ArgumentException($"Product with ID {product.Id} not found.");

            if (string.IsNullOrWhiteSpace(product.Name))
                throw new ArgumentException("Product name is required.");

            if (product.Price <= 0)
                throw new ArgumentException("Price must be greater than zero.");

            existingProduct.Name = product.Name;
            existingProduct.Description = product.Description;
            existingProduct.Price = product.Price;
            existingProduct.StockQuantity = product.StockQuantity;
            existingProduct.IsActive = product.IsActive;
            existingProduct.Barcode = product.Barcode;
            existingProduct.LastModified = DateTime.UtcNow;

            _db.SaveChanges();
        }

        // Delete a product by ID
        public void DeleteProduct(int id)
        {
            var product = _db.Products.FirstOrDefault(p => p.Id == id);
            if (product != null)
            {
                _db.Products.Remove(product);
                _db.SaveChanges();
            }
        }
    }
}
