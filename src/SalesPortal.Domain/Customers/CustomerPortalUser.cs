using System;
using System.Collections.Generic;
using System.Text;

namespace SalesPortal.Domain.Customers
{
    public sealed class CustomerPortalUser
    {
        public string CardCode { get; set; } = string.Empty;
        public string CardName { get; set; } = string.Empty;

        public string UserWeb { get; set; } = string.Empty;
        public string EmailWeb { get; set; } = string.Empty;
        public string PasswordWeb { get; set; } = string.Empty;

        public bool MustChangePassword { get; set; }

        public bool IsActive { get; set; }
    }
}
