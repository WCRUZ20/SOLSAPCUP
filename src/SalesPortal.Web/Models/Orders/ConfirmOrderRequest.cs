using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SalesPortal.Web.Models.Orders
{
    public sealed class ConfirmOrderRequest
    {
        [Required(ErrorMessage = "Seleccione una dirección de envío.")]
        public string ShippingAddressName { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "El comentario no puede exceder 500 caracteres.")]
        public string? Comments { get; set; }

        [Required]
        public List<ConfirmOrderLineRequest> Lines { get; set; } = new();
    }

    public sealed class ConfirmOrderLineRequest
    {
        [Required]
        public string ItemCode { get; set; } = string.Empty;

        [Range(1, 999999, ErrorMessage = "La cantidad debe ser mayor a cero.")]
        public decimal Quantity { get; set; }
    }
}
