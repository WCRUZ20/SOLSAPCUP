using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SalesPortal.Web.Models.Orders
{
    public sealed class OrderListViewModel
    {
        [Display(Name = "Fecha desde")]
        [DataType(DataType.Date)]
        public DateTime? DateFrom { get; set; }

        [Display(Name = "Fecha hasta")]
        [DataType(DataType.Date)]
        public DateTime? DateTo { get; set; }

        [Display(Name = "#Num orden")]
        public int? DocNum { get; set; }

        public IReadOnlyList<OrderListItemViewModel> Orders { get; set; } = Array.Empty<OrderListItemViewModel>();
    }
}
