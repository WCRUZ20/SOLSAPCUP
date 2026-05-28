using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SalesPortal.Web.Models.Orders
{
    public sealed class BulkOrderUploadResponse
    {
        public IReadOnlyList<BulkOrderRowViewModel> Orders { get; set; } = Array.Empty<BulkOrderRowViewModel>();
        public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
    }

    public sealed class BulkOrderProcessRequest
    {
        [Required]
        public List<BulkOrderRowViewModel> Orders { get; set; } = new();
    }

    public sealed class BulkOrderProcessResponse
    {
        public IReadOnlyList<BulkOrderProcessResultViewModel> Results { get; set; } = Array.Empty<BulkOrderProcessResultViewModel>();
    }

    public sealed class BulkOrderProcessResultViewModel
    {
        public string IdOrden { get; set; } = string.Empty;
        public string Result { get; set; } = BulkOrderResultStatuses.NotProcessed;
        public string? Message { get; set; }
        public int? DocEntry { get; set; }
    }

    public sealed class BulkOrderRowViewModel
    {
        public string IdOrden { get; set; } = string.Empty;
        public string ShippingAddressName { get; set; } = string.Empty;
        public string? Comments { get; set; }
        public string Result { get; set; } = BulkOrderResultStatuses.NotProcessed;
        public string? ResultMessage { get; set; }
        public int? DocEntry { get; set; }
        public decimal EstimatedTotal { get; set; }
        public List<string> ValidationMessages { get; set; } = new();
        public List<BulkOrderLineViewModel> Lines { get; set; } = new();
    }

    public sealed class BulkOrderLineViewModel
    {
        public int LineNumber { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountPercent { get; set; }
        public string? TaxCode { get; set; }
        public decimal LineTotal { get; set; }
    }

    public static class BulkOrderResultStatuses
    {
        public const string NotProcessed = "No procesado";
        public const string Created = "Creada";
        public const string Error = "Error";
    }
}
