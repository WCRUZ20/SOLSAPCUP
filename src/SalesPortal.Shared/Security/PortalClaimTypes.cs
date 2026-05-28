using System;
using System.Collections.Generic;
using System.Text;

namespace SalesPortal.Shared.Security
{
    public static class PortalClaimTypes
    {
        public const string CardCode = "CardCode";
        public const string CardName = "CardName";
        public const string TenantCode = "TenantCode";
        public const string MustChangePassword = "MustChangePassword";
        public const string Role = "Role";
    }
}
