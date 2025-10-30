using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xavissa.Database.Models;
using Xavissa.Backend.Services;
using Xavissa.Backend.DTOs;
using Xavissa.Backend.Utilities;

namespace Xavissa.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductController : ControllerBase
    {
        private readonly ProductService _productService;

        public ProductController(ProductService productService)
        {
            _productService = productService;
        }

        // GET: api/Product
        [HttpGet]
        [Authorize] // All roles can view products
        public IActionResult GetAllProducts()
        {
            var products = _productService.GetAllProducts();
            return Ok(products);
        }

        // GET: api/Product/{id}
        [HttpGet("{id}")]
        [Authorize] // All roles can view product details
        public IActionResult GetProduct(int id)
        {
            var product = _productService.GetProductById(id);
            if (product == null) return NotFound();
            return Ok(product);
        }

        // POST: api/Product
        [HttpPost]
        [Authorize(Roles = "Superuser,Admin")]
        public IActionResult CreateProduct([FromBody] ProductCreateDto dto)
        {
            try
            {
                var product = new Product
                {
                    Name = dto.Name,
                    Code = IdGenerator.GenerateId("PROD"),
                    Description = dto.Description,
                    Category = dto.Category,
                    Price = dto.Price,
                    StockQuantity = dto.StockQuantity,
                    IsActive = dto.IsActive
                };
        
                _productService.AddProduct(product);
                return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // PUT: api/Product/{id}
        [HttpPut("{id}")]
        [Authorize(Roles = "Superuser,Admin")]
        public IActionResult UpdateProduct(int id, [FromBody] UpdateProductDto dto)
        {
            try
            {
                var product = _productService.GetProductById(id);
                if (product == null)
                    return NotFound("Product not found.");

                // Apply updates using the DTO
                product.Description = dto.Description ?? product.Description;
                product.Price = dto.Price != 0 ? dto.Price : product.Price;
                product.StockQuantity = dto.StockQuantity != 0 ? dto.StockQuantity : product.StockQuantity;
                product.IsActive = dto.IsActive;

                _productService.UpdateProduct(product);

                return Ok(product);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }


        // DELETE: api/Product/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Superuser,Admin")] // Only admins and superuser can delete
        public IActionResult DeleteProduct(int id)
        {
            try
            {
                _productService.DeleteProduct(id);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
