namespace Cashflow.Service.Transaction.Application.Commands
{
    public interface ICreateTransactionHandler
    {
        Task HandleAsync(CreateTransactionCommand command, CancellationToken cancellationToken = default);
    }
}