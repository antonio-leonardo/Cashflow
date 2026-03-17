namespace Cashflow.Worker.Report
{
    public class TransactionReportDocument
    {
        public Guid Id { get; set; }

        public Guid AccountId { get; set; }

        public decimal Amount { get; set; }

        public string Currency { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}