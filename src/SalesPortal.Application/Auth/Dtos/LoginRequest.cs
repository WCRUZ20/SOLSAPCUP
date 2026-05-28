using System;
using System.Collections.Generic;
using System.Text;

namespace SalesPortal.Application.Auth.Dtos
{
    public sealed class LoginRequest
    {
        public string UserOrEmail { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
    }
}
