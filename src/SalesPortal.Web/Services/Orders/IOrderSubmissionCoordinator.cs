namespace SalesPortal.Web.Services.Orders
{
    public interface IOrderSubmissionCoordinator
    {
        Task<OrderSubmissionExecutionResult> ExecuteOnceAsync(
            string submissionKey,
            Func<CancellationToken, Task<int?>> operation,
            CancellationToken cancellationToken);
    }
}
