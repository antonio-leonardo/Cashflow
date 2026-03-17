namespace Cashflow.Shared.Contracts.Api
{
    /// <summary>
    /// Contrato compartilhado para criação de transação (API / Gateway).
    /// </summary>
    public sealed record CreateTransactionRequest(
        Guid AccountId,
        decimal Amount,
        string Currency,
        int Type);
}