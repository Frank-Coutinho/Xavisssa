namespace Xavissa.Backend.DTOs
{
    public class UpdateProductDto
    {
        public int? TenantId { get; set; }
        public int? CategoryId { get; set; }
        public string? Category { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Brand { get; set; }
        public bool? IsActive { get; set; }
    }
}
