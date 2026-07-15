using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence
{
    public class PaymentsDbContext : DbContext
    {
        public PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : base(options)
        {
        }

        public DbSet<Operation> Operations { get; set; }
        public DbSet<OperationEvent> OperationEvents { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. Configuration Operation
            modelBuilder.Entity<Operation>(entity =>
            {
                entity.ToTable("operations");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.Property(e => e.OperationId).HasColumnName("operation_id").IsRequired().HasMaxLength(100);
                entity.Property(e => e.ProviderPaymentId).HasColumnName("provider_payment_id").HasMaxLength(100);

                entity.Property(e => e.Status).HasColumnName("status").HasConversion<string>();
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

                entity.HasIndex(e => e.OperationId)
                  .IsUnique()
                  .HasDatabaseName("ix_operations_operation_id");

                entity.HasIndex(e => e.ProviderPaymentId)
                      .IsUnique()
                      .HasFilter("provider_payment_id IS NOT NULL")
                      .HasDatabaseName("ix_operations_provider_payment_id");

                entity.HasMany(e => e.Events)
                      .WithOne()
                      .HasForeignKey("OperationId")
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Metadata.FindNavigation(nameof(Operation.Events))!
                      .SetPropertyAccessMode(PropertyAccessMode.Field);
            });

            // 2. Configuration OperationEvent
            modelBuilder.Entity<OperationEvent>(entity =>
            {
                entity.ToTable("operation_events");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.Property(e => e.OperationId).HasColumnName("operation_id").IsRequired();
                entity.Property(e => e.OldStatus).HasColumnName("old_status").HasConversion<string>();
                entity.Property(e => e.NewStatus).HasColumnName("new_status").HasConversion<string>();
                entity.Property(e => e.Reason).HasColumnName("reason").IsRequired().HasMaxLength(255);
                entity.Property(e => e.Timestamp).HasColumnName("timestamp");
            });
        }
    }
}