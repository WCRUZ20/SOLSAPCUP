using Microsoft.AspNetCore.Identity;
using SalesPortal.Application.Abstractions.Security;
using System;
using System.Collections.Generic;
using System.Text;

namespace SalesPortal.Infrastructure.Security
{
    public sealed class PasswordHasher : IPasswordHasher
    {
        private readonly PasswordHasher<object> _hasher = new();

        public string Hash(string password)
        {
            return _hasher.HashPassword(new object(), password);
        }

        public bool Verify(string password, string storedPassword)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(storedPassword))
                return false;

            if (storedPassword.StartsWith("AQAAAA", StringComparison.Ordinal))
            {
                var result = _hasher.VerifyHashedPassword(
                    new object(),
                    storedPassword,
                    password);

                return result == PasswordVerificationResult.Success ||
                       result == PasswordVerificationResult.SuccessRehashNeeded;
            }

            return password == storedPassword;
        }
    }
}
