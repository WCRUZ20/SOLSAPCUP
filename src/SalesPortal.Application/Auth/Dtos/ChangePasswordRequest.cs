using System;
using System.Collections.Generic;
using System.Text;

namespace SalesPortal.Application.Auth.Dtos
{
    public sealed class ChangePasswordRequest
    {
        public string CardCode { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
    }
}
