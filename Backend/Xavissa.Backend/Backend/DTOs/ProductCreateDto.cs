namespace Xavissa.Backend.DTOs
{
    public class ProductCreateDto
    {
        public int? TenantId { get; set; }
        public int? CategoryId { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Brand { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
