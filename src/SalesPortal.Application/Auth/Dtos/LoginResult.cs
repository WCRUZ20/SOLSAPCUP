using System;
using System.Collections.Generic;
using System.Text;

namespace SalesPortal.Application.Auth.Dtos
{
    public sealed class LoginResult
    {
        public string CardCode { get; set; } = string.Empty;
        public string CardName { get; set; } = string.Empty;
        public string UserWeb { get; set; } = string.Empty;
        public string EmailWeb { get; set; } = string.Empty;
        public string TenantCode { get; set; } = string.Empty;
        public bool MustChangePassword { get; set; }
    }
}
