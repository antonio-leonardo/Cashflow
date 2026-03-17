using Microsoft.EntityFrameworkCore;

namespace Cashflow.Service.Transaction.Infrastructure.Persistence
{
    public sealed class TransactionDbContext : DbContext
    {
        public TransactionDbContext(DbContextOptions<TransactionDbContext> options)
            : base(options)
        {
        }

        public DbSet<TransactionEntity> Transactions => Set<TransactionEntity>();
        public DbSet<OutboxEventEntity> OutboxEvents => Set<OutboxEventEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TransactionEntity>(e =>
            {
                e.ToTable("Transactions");
                e.HasKey(x => x.Id);
                e.Property(x => x.Currency).HasMaxLength(3);
            });

            modelBuilder.Entity<OutboxEventEntity>(e =>
            {
                e.ToTable("OutboxEvents");
                e.HasKey(x => x.EventId);
                e.Property(x => x.EventType).HasMaxLength(256);
                e.Property(x => x.Payload).IsRequired();
                e.Property(x => x.CreatedAt).IsRequired();
                e.Property(x => x.ProcessedAt);
                e.Property(x => x.RetryCount).IsRequired();
            });
        }
    }
}