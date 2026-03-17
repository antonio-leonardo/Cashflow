using Microsoft.EntityFrameworkCore;

namespace Cashflow.Service.Transaction.Infrastructure.Persistence
{
    public sealed class IdempotencyDbContext : DbContext
    {
        public IdempotencyDbContext(DbContextOptions<IdempotencyDbContext> options)
            : base(options)
        {
        }

        public DbSet<ProcessedEventEntity> ProcessedEvents => Set<ProcessedEventEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProcessedEventEntity>(e =>
            {
                e.ToTable("ProcessedEvents");
                e.HasKey(x => new { x.EventId, x.ConsumerName });
                e.Property(x => x.ConsumerName).HasMaxLength(256);
            });
        }
    }
}