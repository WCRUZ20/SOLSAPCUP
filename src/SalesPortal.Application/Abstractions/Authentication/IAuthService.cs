using SalesPortal.Application.Auth.Dtos;
using SalesPortal.Application.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace SalesPortal.Application.Abstractions.Authentication
{
    public interface IAuthService
    {
        Task<Result<LoginResult>> LoginAsync(
            LoginRequest request,
            CancellationToken cancellationToken);

        Task<Result<RegisterResult>> RegisterAsync(
            RegisterRequest request,
            CancellationToken cancellationToken);

        Task<Result> ChangePasswordAsync(
            ChangePasswordRequest request,
            CancellationToken cancellationToken);
    }
}
