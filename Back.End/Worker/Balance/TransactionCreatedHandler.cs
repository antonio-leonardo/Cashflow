using Cashflow.Back.End.Service.Transaction.Domain;

namespace Cashflow.Back.End.Worker.Balance
{
    public class TransactionCreatedHandler
    {
        private readonly RedisBalanceRepository _repository;

        public TransactionCreatedHandler(RedisBalanceRepository repository)
        {
            _repository = repository;
        }

        public async Task HandleAsync(TransactionCreatedEventV1 evt, CancellationToken ct)
        {
            await _repository.ApplyAsync(evt);
        }
    }
}