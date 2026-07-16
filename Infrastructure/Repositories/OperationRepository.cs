using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class OperationRepository : IOperationRepository
{
    private readonly PaymentsDbContext _context;

    public OperationRepository(PaymentsDbContext context)
    {
        _context = context;
    }

    public async Task<Operation?> GetByIdAsync(string operationId, CancellationToken ct = default)
    {
        // Tracking is required here to allow status updates and concurrency checks (RowVersion)
        return await _context.Operations
            .Include(o => o.Events)
            .FirstOrDefaultAsync(o => o.OperationId == operationId, ct);
    }

    public async Task AddAsync(Operation operation, CancellationToken ct = default)
    {
        await _context.Operations.AddAsync(operation, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Operation>> GetPendingSubmissionsAsync(CancellationToken ct = default)
    {
        // Fetch operations stuck in PROCESSING state without a provider response yet
        return await _context.Operations
            .Include(o => o.Events)
            .Where(o => o.Status == OperationStatus.Processing && o.ProviderPaymentId == null)
            .ToListAsync(ct);
    }
}