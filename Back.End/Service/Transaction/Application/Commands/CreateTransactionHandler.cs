using Cashflow.Shared.Logging;

namespace Cashflow.Service.Transaction.Application.Commands
{
    public sealed class CreateTransactionHandler : ICreateTransactionHandler
    {
        private readonly ITransactionRepository _repository;
        private readonly ILogService _logService;
        private readonly string _serviceName = "TransactionService";

        public CreateTransactionHandler(
            ITransactionRepository repository,
            ILogService logService)
        {
            _repository = repository;
            _logService = logService;
        }

        public async Task HandleAsync(CreateTransactionCommand command, CancellationToken cancellationToken = default)
        {
            var transaction = Cashflow.Service.Transaction.Domain.Transaction.Create(
                command.TransactionId,
                command.AccountId,
                command.Amount,
                command.Currency,
                command.Type);

            await _repository.AddAsync(transaction, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);

            var logContext = new LogContext(
                ServiceName: _serviceName,
                CorrelationId: command.CorrelationId,
                TransactionId: command.TransactionId.ToString(),
                UserId: command.UserId);

            _logService.Log(
                LogLevel.Information,
                "Transaction created and event published.",
                logContext,
                additionalData: new Dictionary<string, object>
                {
                    ["AccountId"] = command.AccountId,
                    ["Amount"] = command.Amount,
                    ["Currency"] = command.Currency,
                    ["Type"] = command.Type.ToString()
                });
        }
    }
}