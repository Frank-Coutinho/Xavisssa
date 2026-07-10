using System;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Mappers
{
    public static class ProductMapper
    {
        public static Product FromReadDto(ProductReadDto dto)
        {
            return new Product
            {
                OnlineId = dto.Id,
                TenantId = dto.TenantId,
                StoreId = dto.StoreId,
                AssignmentId = dto.AssignmentId,
                VariantId = dto.VariantId,
                CategoryId = dto.CategoryId,
                Code = dto.Code,
                Barcode = dto.Barcode,
                Name = dto.Name,
                SKU = dto.SKU,
                Label = dto.Label ?? string.Empty,
                ImageUrl = dto.ImageUrl,
                Description = dto.Description,
                Category = dto.Category,
                Brand = dto.Brand ?? string.Empty,
                Price = dto.Price,
                StockQuantity = dto.StockQuantity,
                IsActive = dto.IsActive,
                VariantCount = dto.VariantCount,
                CreatedAt = dto.CreatedAt ?? DateTime.UtcNow,
                UpdatedAt = dto.UpdatedAt ?? DateTime.UtcNow,
            };
        }
    }
}

