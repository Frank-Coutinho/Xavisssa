using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xavissa.Backend.DTOs;
using Xavissa.Backend.Security;
using Xavissa.Backend.Services;
using Xavissa.Database.Models;
using Xavissa.Database.ViewModels;

namespace Xavissa.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Route("api/v1/sales")]
    [Authorize]
    public class SalesController : ControllerBase
    {
        private readonly SalesService _salesService;
        private readonly TenantAccessService _tenantAccess;

        public SalesController(
            SalesService salesService,
            TenantAccessService tenantAccess
        )
        {
            _salesService = salesService;
            _tenantAccess = tenantAccess;
        }

        [HttpGet]
        public async Task<ActionResult<List<SaleReadDto>>> GetAll()
        {
            var sales = await _salesService.GetAllSalesAsync();
            return Ok(sales.Select(MapSale));
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<SaleReadDto>> GetById(int id)
        {
            var sale = await _salesService.GetSaleByIdAsync(id);
            return sale == null ? NotFound() : Ok(MapSale(sale));
        }

        [HttpGet("summary")]
        public async Task<ActionResult<SalesReportViewModel>> GetSummary(
            DateTime? start = null,
            DateTime? end = null
        )
        {
            return Ok(await _salesService.GetSalesSummaryAsync(start, end));
        }

        [HttpPost]
        public async Task<ActionResult<SaleReadDto>> Create([FromBody] SaleCreateDto dto)
        {
            var storeRequirement = _tenantAccess.RequireSelectedStore();
            if (storeRequirement != null)
                return storeRequirement.Result!;

            var tenantId = dto.TenantId ?? _tenantAccess.SelectedTenantId;
            if (!tenantId.HasValue)
                return BadRequest("Tenant is required.");
            if (!_tenantAccess.CurrentUserId.HasValue)
                return BadRequest("Invalid user claim.");

            var sale = await _salesService.CreateSaleAsync(
                dto.SaleItems,
                dto.SalePayments,
                _tenantAccess.CurrentUserId.Value,
                tenantId.Value,
                _tenantAccess.SelectedStoreId!.Value,
                dto.Discount ?? 0,
                dto.SyncId,
                dto.SourceDeviceId,
                dto.ClientCreatedAt,
                dto.ClientUpdatedAt
            );

            return CreatedAtAction(nameof(GetById), new { id = sale.Id }, MapSale(sale));
        }

        [HttpPost("{id:int}/soft-delete")]
        public async Task<ActionResult> SoftDelete(int id, [FromBody] DeleteSaleRequest request)
        {
            var result = await _salesService.SoftDeleteSaleAsync(id, request.Reason ?? string.Empty);
            return result ? Ok("Sale archived and soft-deleted successfully.") : NotFound();
        }

        [HttpPost("{saleId:int}/items/{saleItemId:int}/soft-delete")]
        public async Task<ActionResult> SoftDeleteSaleItem(
            int saleId,
            int saleItemId,
            [FromBody] DeleteSaleRequest request)
        {
            var result = await _salesService.SoftDeleteSaleItemAsync(
                saleId,
                saleItemId,
                request.Reason ?? string.Empty);
            return result ? Ok("Sale item archived and soft-deleted successfully.") : NotFound();
        }

        [HttpPost("{id:int}/refund")]
        public async Task<ActionResult> Refund(int id, [FromBody] RefundRequest request)
        {
            try
            {
                var result = await _salesService.RefundSaleAsync(id, request.Reason ?? "Refunded");
                return result ? Ok() : NotFound();
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
            }
        }

        [HttpPost("{id:int}/void")]
        public async Task<ActionResult> Void(int id, [FromBody] VoidSaleRequest request)
        {
            try
            {
                var result = await _salesService.VoidSaleAsync(id, request.Reason ?? "Voided");
                return result ? Ok() : NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
            }
        }

        [HttpPost("{saleId:int}/items/{saleItemId:int}/refund")]
        public async Task<ActionResult> RefundSaleItem(
            int saleId,
            int saleItemId,
            [FromBody] RefundSaleItemRequest request)
        {
            try
            {
                var result = await _salesService.RefundSaleItemAsync(
                    saleId,
                    saleItemId,
                    request.Quantity,
                    request.Reason ?? "Refunded");
                return result ? Ok() : NotFound();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
            }
        }

        private static SaleReadDto MapSale(Sale sale)
        {
            return new SaleReadDto
            {
                Id = sale.Id,
                OnlineId = sale.Id,
                SyncId = sale.SyncId,
                TenantId = sale.TenantId ?? 0,
                StoreId = sale.StoreId,
                SourceDeviceId = sale.SourceDeviceId,
                ClientCreatedAt = sale.ClientCreatedAt,
                ClientUpdatedAt = sale.ClientUpdatedAt,
                LastSyncedAt = sale.LastSyncedAt,
                CreatedAt = sale.CreatedAt,
                UpdatedAt = sale.UpdatedAt,
                SaleDate = sale.SaleDate,
                TotalAmount = sale.TotalAmount,
                Discount = sale.Discount,
                TotalPaid = sale.Payments.Sum(x => x.Amount),
                PaymentSummary = SalesService.BuildPaymentSummary(sale.Payments),
                PaymentStatus = sale.PaymentStatus,
                ChangeGiven = sale.ChangeGiven,
                ReceiptNumber = sale.ReceiptNumber,
                Status = sale.Status,
                IsRefunded = sale.IsRefunded,
                IsVoided = sale.IsVoided,
                RefundReason = sale.RefundReason,
                VoidedAt = sale.VoidedAt,
                VoidedByUserId = sale.VoidedByUserId,
                VoidReason = sale.VoidReason,
                CashRegisterSessionId = sale.CashRegisterSessionId,
                CashRegisterTrackingMode = sale.CashRegisterTrackingMode,
                HasUntrackedCashPayment = sale.HasUntrackedCashPayment,
                SalePayments = sale.Payments.Select(payment => new SalePaymentReadDto
                {
                    Id = payment.Id,
                    OnlineId = payment.Id,
                    SyncId = payment.SyncId,
                    SourceDeviceId = payment.SourceDeviceId,
                    ClientCreatedAt = payment.ClientCreatedAt,
                    ClientUpdatedAt = payment.ClientUpdatedAt,
                    LastSyncedAt = payment.LastSyncedAt,
                    CreatedAt = payment.CreatedAt,
                    PaymentMethod = payment.PaymentMethod,
                    Amount = payment.Amount,
                    CashRegisterSessionId = payment.CashRegisterSessionId,
                    ReferenceNumber = payment.ReferenceNumber,
                    Notes = payment.Notes,
                }).ToList(),
                SaleItems = sale
                    .SaleItems.Select(si => new SaleItemReadDto
                    {
                        Id = si.Id,
                        OnlineId = si.Id,
                        SyncId = si.SyncId,
                        TenantId = si.TenantId ?? 0,
                        StoreId = si.StoreId,
                        SourceDeviceId = si.SourceDeviceId,
                        ClientCreatedAt = si.ClientCreatedAt,
                        ClientUpdatedAt = si.ClientUpdatedAt,
                        LastSyncedAt = si.LastSyncedAt,
                        CreatedAt = si.CreatedAt,
                        UpdatedAt = si.UpdatedAt,
                        ProductId = si.Variant?.ProductId ?? 0,
                        VariantId = si.VariantId,
                        ProductName = si.Variant?.Product?.Name ?? "Unknown Product",
                        Quantity = si.Quantity,
                        UnitPrice = si.UnitPrice,
                        Subtotal = si.UnitPrice * si.Quantity,
                        ProductCategory = si.Variant?.Product?.CategoryNavigation?.Name ?? string.Empty,
                        IsRefunded = si.IsRefunded,
                        RefundedQuantity = si.RefundedQuantity,
                        RefundableQuantity = Math.Max(si.Quantity - si.RefundedQuantity, 0),
                        RefundReason = si.RefundReason,
                    })
                    .ToList(),
            };
        }
    }

    public class RefundRequest
    {
        public string? Reason { get; set; }
    }

    public class RefundSaleItemRequest
    {
        public int Quantity { get; set; }
        public string? Reason { get; set; }
    }

    public class DeleteSaleRequest
    {
        public string? Reason { get; set; }
    }

    public class VoidSaleRequest
    {
        public string? Reason { get; set; }
    }
}
