namespace Cashflow.Worker.Balance
{
    public interface IBalanceProjectionRepository
    {
        /// <summary>
        /// Atomically applies a transaction event to the balance projection.
        /// Returns true when the event was applied; false when it was a duplicate (idempotency).
        /// </summary>
        Task<bool> ApplyAsync(
            Cashflow.Service.Transaction.Domain.TransactionCreatedEventV1 evt,
            string consumerName,
            string idempotencyKey,
            TimeSpan processedEventTtl);
    }
}
