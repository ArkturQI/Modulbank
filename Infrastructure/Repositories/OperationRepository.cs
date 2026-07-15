using Application.Interfaces;
using Domain.Entities;
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
}