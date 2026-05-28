using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace SalesPortal.Web.Services.Orders
{
    public sealed class OrderSubmissionCoordinator : IOrderSubmissionCoordinator
    {
        private static readonly TimeSpan CompletedSubmissionLifetime = TimeSpan.FromSeconds(30);

        private readonly IMemoryCache _completedSubmissions;
        private readonly ConcurrentDictionary<string, Lazy<Task<int?>>> _inFlightSubmissions = new(StringComparer.Ordinal);

        public OrderSubmissionCoordinator(IMemoryCache completedSubmissions)
        {
            _completedSubmissions = completedSubmissions;
        }

        public async Task<OrderSubmissionExecutionResult> ExecuteOnceAsync(
            string submissionKey,
            Func<CancellationToken, Task<int?>> operation,
            CancellationToken cancellationToken)
        {
            var completedKey = BuildCompletedKey(submissionKey);

            if (_completedSubmissions.TryGetValue<CompletedSubmission>(completedKey, out var completedSubmission))
            {
                return new OrderSubmissionExecutionResult(
                    OrderSubmissionExecutionStatus.DuplicateCompleted,
                    completedSubmission.DocEntry);
            }

            var submission = new Lazy<Task<int?>>(
                () => operation(cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication);

            var activeSubmission = _inFlightSubmissions.GetOrAdd(submissionKey, submission);

            if (!ReferenceEquals(activeSubmission, submission))
            {
                var docEntry = await activeSubmission.Value;
                return new OrderSubmissionExecutionResult(
                    OrderSubmissionExecutionStatus.DuplicateCompleted,
                    docEntry);
            }

            try
            {
                var docEntry = await activeSubmission.Value;
                _completedSubmissions.Set(completedKey, new CompletedSubmission(docEntry), CompletedSubmissionLifetime);

                return new OrderSubmissionExecutionResult(
                    OrderSubmissionExecutionStatus.Created,
                    docEntry);
            }
            finally
            {
                _inFlightSubmissions.TryRemove(new KeyValuePair<string, Lazy<Task<int?>>>(submissionKey, activeSubmission));
            }
        }

        private static string BuildCompletedKey(string submissionKey) => $"orders:submitted:{submissionKey}";

        private sealed record CompletedSubmission(int? DocEntry);
    }
}
