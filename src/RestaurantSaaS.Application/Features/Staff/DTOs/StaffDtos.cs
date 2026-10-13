namespace RestaurantSaaS.Application.Features.Staff.DTOs;

/// <summary>Staff roster projection. Never exposes security stamps or tenant internals.</summary>
public sealed record StaffMemberDto(
    Guid MembershipId,
    Guid UserId,
    string Email,
    string? UserName,
    string RoleName,
    string Status,
    DateTime JoinedAt,
    DateTime CreatedAt);

/// <summary>
/// Invite/add staff. TenantId is NOT accepted — the current tenant context is
/// authoritative. RoleName must be a tenant-level role; platform roles cannot
/// be granted here (enforced by validator + use case).
/// </summary>
public sealed record InviteStaffRequest(string Email, string RoleName);

/// <summary>Change a member's tenant role. Only allowed tenant roles accepted.</summary>
public sealed record AssignStaffRoleRequest(string RoleName);

/// <summary>Active | Suspended | Removed (Removed = end membership, keeps Identity account & other tenants).</summary>
public sealed record UpdateStaffStatusRequest(string Status);
