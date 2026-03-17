namespace Cashflow.Service.Transaction.Infrastructure.Persistence
{
    public sealed class TransactionEntity
    {
        public Guid Id { get; set; }
        public Guid AccountId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public int Type { get; set; }
        public int Status { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}