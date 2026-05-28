using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SalesPortal.Web.Models.Orders
{
    public sealed class OrderCreateViewModel
    {
        [Display(Name = "Dirección de envío")]
        public string? ShippingAddressName { get; set; }

        [Display(Name = "Comentarios")]
        [StringLength(500, ErrorMessage = "El comentario no puede exceder 500 caracteres.")]
        public string? Comments { get; set; }

        public string SubmissionToken { get; set; } = string.Empty;

        public IReadOnlyList<SalesItemOptionViewModel> Items { get; set; } = Array.Empty<SalesItemOptionViewModel>();
        public IReadOnlyList<ShippingAddressOptionViewModel> ShippingAddresses { get; set; } = Array.Empty<ShippingAddressOptionViewModel>();
    }
}
