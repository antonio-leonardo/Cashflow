using Cashflow.Service.Transaction.Domain;
using Microsoft.Extensions.Logging;

namespace Cashflow.Worker.Balance
{
    public class TransactionCreatedHandler
    {
        private readonly RedisBalanceRepository _repository;
        private readonly ILogger<TransactionCreatedHandler> _logger;
        private const string ConsumerName = "balance-worker";
        private static readonly TimeSpan ProcessedEventTtl = TimeSpan.FromDays(30);

        public TransactionCreatedHandler(
            RedisBalanceRepository repository,
            ILogger<TransactionCreatedHandler> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task HandleAsync(
            TransactionCreatedEventV1 evt,
            string? idempotencyKey,
            CancellationToken ct)
        {
            var effectiveIdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey)
                ? evt.EventId.ToString("N")
                : idempotencyKey;

            var applied = await _repository.ApplyAsync(
                evt,
                ConsumerName,
                effectiveIdempotencyKey,
                ProcessedEventTtl);

            if (!applied)
            {
                _logger.LogInformation(
                    "Duplicate event ignored by read-side idempotency. EventId={EventId}, AccountId={AccountId}, Key={IdempotencyKey}.",
                    evt.EventId,
                    evt.AccountId,
                    effectiveIdempotencyKey);
            }
        }
    }
}
