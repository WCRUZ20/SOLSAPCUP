namespace SalesPortal.Web.Services.Orders
{
    public sealed record OrderSubmissionExecutionResult(
        OrderSubmissionExecutionStatus Status,
        int? DocEntry);
}
