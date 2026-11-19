using BahaGuard.Application.DTOs;

namespace BahaGuard.Application.Interfaces;

public interface IPropertyAuditService
{
    Task<PropertyAuditResponseDto> AuditPropertyAsync(
        PropertyAuditQuery query,
        CancellationToken cancellationToken = default);
}
