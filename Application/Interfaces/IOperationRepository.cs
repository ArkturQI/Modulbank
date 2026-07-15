using Domain.Entities;

namespace Application.Interfaces
{
    public interface IOperationRepository
    {
        Task<Operation?> GetByIdAsync(string operationId, CancellationToken ct = default);
        Task AddAsync(Operation operation, CancellationToken ct = default);
        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
