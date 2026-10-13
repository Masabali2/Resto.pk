using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.Staff.DTOs;

namespace RestaurantSaaS.Application.Features.Staff;

/// <summary>
/// Staff membership use cases. Tenant scope always comes from ITenantContext;
/// role resolution always comes from StaffMembership (never AspNetUserRoles).
/// </summary>
public interface IStaffService
{
    Task<PagedResult<StaffMemberDto>> ListAsync(int page, int pageSize, CancellationToken ct = default);

    Task<StaffMemberDto> GetByIdAsync(Guid membershipId, CancellationToken ct = default);

    /// <summary>
    /// Secure provisioning flow (no email delivery in this phase — documented dependency):
    /// creates-or-joins an Identity user WITHOUT password and attaches an ACTIVE
    /// membership with the requested tenant role. The invitee completes signup via
    /// the Identity forgot-password/reset flow to set their own credentials.
    /// </summary>
    Task<StaffMemberDto> InviteAsync(InviteStaffRequest request, CancellationToken ct = default);

    Task<StaffMemberDto> AssignRoleAsync(Guid membershipId, string roleName, CancellationToken ct = default);

    Task<StaffMemberDto> ChangeStatusAsync(Guid membershipId, string status, CancellationToken ct = default);

    Task RemoveAsync(Guid membershipId, CancellationToken ct = default);
}
