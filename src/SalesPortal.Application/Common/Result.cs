using System;
using System.Collections.Generic;
using System.Text;

namespace SalesPortal.Application.Common
{
    public class Result
    {
        public bool IsSuccess { get; protected set; }
        public string Message { get; protected set; } = string.Empty;

        public bool IsFailure => !IsSuccess;

        public static Result Success(string message = "")
        {
            return new Result
            {
                IsSuccess = true,
                Message = message
            };
        }

        public static Result Failure(string message)
        {
            return new Result
            {
                IsSuccess = false,
                Message = message
            };
        }
    }

    public sealed class Result<T> : Result
    {
        public T? Data { get; private set; }

        public static Result<T> Success(T data, string message = "")
        {
            return new Result<T>
            {
                IsSuccess = true,
                Message = message,
                Data = data
            };
        }

        public new static Result<T> Failure(string message)
        {
            return new Result<T>
            {
                IsSuccess = false,
                Message = message,
                Data = default
            };
        }
    }
}
