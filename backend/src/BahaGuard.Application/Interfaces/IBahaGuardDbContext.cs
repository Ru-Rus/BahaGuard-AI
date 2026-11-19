using BahaGuard.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BahaGuard.Application.Interfaces;

public interface IBahaGuardDbContext
{
    DbSet<FloodReport> FloodReports { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
