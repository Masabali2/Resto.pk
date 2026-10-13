//using Microsoft.AspNetCore.Identity;
//using Microsoft.EntityFrameworkCore;
//using RestaurantSaaS.Application.Common.Authorization;
//using RestaurantSaaS.Application.Common.Exceptions;
//using RestaurantSaaS.Application.Common.Interfaces;
//using RestaurantSaaS.Application.Common.Models;
//using RestaurantSaaS.Application.Features.Staff;
//using RestaurantSaaS.Application.Features.Staff.DTOs;
//using RestaurantSaaS.Domain.Entities.Identity;
//using RestaurantSaaS.Domain.Enums;
//using RestaurantSaaS.Domain.Exceptions;
//using RestaurantSaaS.Infrastructure.Persistence;

//namespace RestaurantSaaS.Infrastructure.Services;

///// <summary>
///// Staff membership management. Tenant scope comes exclusively from
///// ITenantContext; roles come exclusively from StaffMembership (AspNetUserRoles
///// remains reserved for platform roles). Removing a membership NEVER touches the
///// Identity account or the user's memberships in OTHER tenants.
///// </summary>
//public sealed class StaffService(
//    ApplicationDbContext db,
//    UserManager<User> userManager,
//    ICurrentUserService currentUser,
//    ITenantContext tenantContext,
//    ITenantMembershipService membershipService) : IStaffService
//{
//    private static readonly string[] AssignableTenantRoles =
//    [
//        Permissions.TenantRoles.Owner,
//        Permissions.TenantRoles.Manager,
//        Permissions.TenantRoles.Kitchen,
//        Permissions.TenantRoles.Cashier,
//        Permissions.TenantRoles.Waiter
//    ];

//    public async Task<PagedResult<StaffMemberDto>> ListAsync(int page, int pageSize, CancellationToken ct = default)
//    {
//        await RequirePermissionAsync(Permissions.StaffView, ct);

//        page = Math.Max(1, page);
//        pageSize = Math.Clamp(pageSize, 1, 100);

//        var query = db.StaffMemberships.AsNoTracking()
//            .Where(m => m.Status != MembershipStatus.Removed)
//            .OrderBy(m => m.JoinedAt);

//        var total = await query.CountAsync(ct);
//        var rows = await query
//            .Skip((page - 1) * pageSize)
//            .Take(pageSize)
//            .Select(m => new
//            {
//                m.Id, m.UserId, m.User.Email, m.User.UserName,
//                RoleName = m.Role.Name, Status = m.Status, m.JoinedAt, m.CreatedAt
//            })
//            .ToListAsync(ct);

//        return new PagedResult<StaffMemberDto>
//        {
//            Items = rows.Select(r => new StaffMemberDto(
//                r.Id, r.UserId, r.Email ?? string.Empty, r.UserName,
//                r.RoleName!, r.Status.ToString(), r.JoinedAt, r.CreatedAt)).ToList(),
//            Page = page, PageSize = pageSize, TotalCount = total
//        };
//    }

//    public async Task<StaffMemberDto> GetByIdAsync(Guid membershipId, CancellationToken ct = default)
//    {
//        await RequirePermissionAsync(Permissions.StaffView, ct);

//        var m = await LoadOwnedMembershipAsync(membershipId, ct);
//        await AttachUserAndRoleAsync(m, ct);

//        return ToDto(m);
//    }

//    /// <inheritdoc />
//    public async Task<StaffMemberDto> InviteAsync(InviteStaffRequest request, CancellationToken ct = default)
//    {
//        // Only Owner/Manager hold Staff.Invite; role check happens here too, not only in policy.
//        await RequirePermissionAsync(Permissions.StaffInvite, ct);

//        if (!AssignableTenantRoles.Contains(request.RoleName))
//            throw new DomainException("STAFF_ROLE_INVALID", $"'{request.RoleName}' is not an assignable tenant role.");

//        var email = request.Email.Trim().ToLowerInvariant();
//        var tenantId = tenantContext.GetRequiredTenantId();

//        // Create-or-join: existing accounts are reused — no duplicate Identity users,
//        // no password generated/reset by us (invitee sets credentials via reset flow).
//        var user = await userManager.FindByEmailAsync(email);
//        if (user is null)
//        {
//            user = new User
//            {
//                UserName = email,
//                Email = email,
//                EmailConfirmed = false, // never assume verification (Phase-2 #17)
//                CreatedAt = DateTime.UtcNow
//            };

//            var created = await userManager.CreateAsync(user); // NO password — pending-invitation account
//            if (!created.Succeeded)
//            {
//                throw new ConflictException(string.Join("; ", created.Errors.Select(e => e.Description)));
//            }
//        }

//        var roleId = await GetRequiredRoleIdAsync(request.RoleName, ct);

//        StaffMembership membership;
//        try
//        {
//            membership = StaffMembership.Create(tenantId, user.Id, roleId);
//            db.StaffMemberships.Add(membership);
//            await db.SaveChangesAsync(ct); // UNIQUE(TenantId, UserId) rejects duplicates at the DB
//        }
//        catch (DbUpdateException ex) when (IsDuplicate(ex))
//        {
//            throw new ConflictException($"A membership already exists for '{email}' in this tenant.");
//        }

//        await AttachUserAndRoleAsync(membership, ct);
//        return ToDto(membership);
//    }

//    public async Task<StaffMemberDto> AssignRoleAsync(Guid membershipId, string roleName, CancellationToken ct = default)
//    {
//        // Role assignment is the most privileged staff operation: Staff.AssignRole (Owner-only).
//        await RequirePermissionAsync(Permissions.StaffAssignRole, ct);

//        if (!AssignableTenantRoles.Contains(roleName))
//            throw new DomainException("STAFF_ROLE_INVALID", $"'{roleName}' is not an assignable tenant role.");

//        var actorUserId = currentUser.UserId!.Value;
//        var target = await LoadOwnedMembershipAsync(membershipId, ct);

//        // SECURITY: nobody escalates their OWN role through this endpoint — even an
//        // Owner cannot self-promote/demote; another owner must do it. Prevents a
//        // stolen session from locking others out by rewriting its own privileges.
//        if (target.UserId == actorUserId)
//            throw new ForbiddenException("You cannot change your own role. Ask another owner.");

//        var newRoleId = await GetRequiredRoleIdAsync(roleName, ct);

//        // Final-owner protection: demoting the last active Owner would orphan the tenant.
//        if (await IsCurrentlyLastActiveOwnerAsync(target, ct) && roleName != Permissions.TenantRoles.Owner)
//            throw new ConflictException("This member is the last active owner. Promote another owner first.");

//        target.ChangeRole(newRoleId);
//        await db.SaveChangesAsync(ct);

//        await AttachUserAndRoleAsync(target, ct);
//        return ToDto(target);
//    }

//    public async Task<StaffMemberDto> ChangeStatusAsync(Guid membershipId, string status, CancellationToken ct = default)
//    {
//        await RequirePermissionAsync(Permissions.StaffUpdate, ct);

//        var actorUserId = currentUser.UserId!.Value;
//        var target = await LoadOwnedMembershipAsync(membershipId, ct);

//        switch (status)
//        {
//            case nameof(MembershipStatus.Active):
//                if (target.UserId == actorUserId)
//                    throw new ForbiddenException("You cannot modify your own membership.");
//                target.Reactivate();
//                break;

//            case nameof(MembershipStatus.Suspended):
//                if (target.UserId == actorUserId)
//                    throw new ForbiddenException("You cannot suspend your own membership.");
//                await EnsureNotLastActiveOwnerAsync(target, ct);
//                target.Suspend();
//                break;

//            case nameof(MembershipStatus.Removed):
//                // Removal goes through RemoveAsync semantics but is allowed here too.
//                await RemoveInternalAsync(target, actorUserId, ct);
//                break;

//            default:
//                throw new DomainException("STAFF_STATUS_INVALID", "Status must be Active, Suspended or Removed.");
//        }

//        await db.SaveChangesAsync(ct);
//        await AttachUserAndRoleAsync(target, ct);
//        return ToDto(target);
//    }

//    public async Task RemoveAsync(Guid membershipId, CancellationToken ct = default)
//    {
//        await RequirePermissionAsync(Permissions.StaffRemove, ct);

//        var actorUserId = currentUser.UserId!.Value;
//        var target = await LoadOwnedMembershipAsync(membershipId, ct);

//        await RemoveInternalAsync(target, actorUserId, ct);
//        await db.SaveChangesAsync(ct);
//    }

//    // ---------------- helpers ----------------

//    private async Task RemoveInternalAsync(StaffMembership target, Guid actorUserId, CancellationToken ct)
//    {
//        if (target.UserId == actorUserId)
//            throw new ForbiddenException("You cannot remove your own membership. Another owner must do it.");

//        await EnsureNotLastActiveOwnerAsync(target, ct);

//        // Ends ONLY this tenant's membership. The Identity account and every other
//        // tenant's membership remain untouched (multi-tenant safety).
//        target.Remove();
//        _ = ct;
//    }

//    private async Task EnsureNotLastActiveOwnerAsync(StaffMembership target, CancellationToken ct)
//    {
//        await AttachUserAndRoleAsync(target, ct);
//        if (target.Status != MembershipStatus.Active) return;
//        if (target.Role?.Name != Permissions.TenantRoles.Owner) return;

//        var tenantId = tenantContext.GetRequiredTenantId();
//        var otherActiveOwners = await db.StaffMemberships.AsNoTracking()
//            .CountAsync(m =>
//                m.TenantId == tenantId &&
//                m.Id != target.Id &&
//                m.Status == MembershipStatus.Active &&
//                m.RoleId == target.RoleId, ct);

//        if (otherActiveOwners == 0)
//            throw new ConflictException("This member is the last active owner. Promote another owner first.");
//    }

//    private async Task<bool> IsCurrentlyLastActiveOwnerAsync(StaffMembership target, CancellationToken ct)
//    {
//        await AttachUserAndRoleAsync(target, ct);
//        if (target.Role?.Name != Permissions.TenantRoles.Owner) return false;

//        var tenantId = tenantContext.GetRequiredTenantId();
//        var otherActiveOwners = await db.StaffMemberships.AsNoTracking()
//            .CountAsync(m =>
//                m.TenantId == tenantId &&
//                m.Id != target.Id &&
//                m.Status == MembershipStatus.Active &&
//                m.RoleId == target.RoleId, ct);

//        return otherActiveOwners == 0;
//    }

//    private async Task RequirePermissionAsync(string permission, CancellationToken ct)
//    {
//        var userId = currentUser.UserId ?? throw new NotAuthenticatedException();
//        var tenantId = tenantContext.GetRequiredTenantId();

//        var membership = await membershipService.GetMembershipAsync(userId, tenantId, ct)
//            ?? throw new CrossTenantAccessException();

//        if (membership.Status != (int)MembershipStatus.Active)
//            throw new CrossTenantAccessException();

//        if (!Permissions.RolePermissions.TryGetValue(membership.RoleName, out var perms) ||
//            !perms.Contains(permission))
//        {
//            throw new ForbiddenException($"Your role '{membership.RoleName}' cannot perform this operation.");
//        }
//    }

//    private async Task<StaffMembership> LoadOwnedMembershipAsync(Guid membershipId, CancellationToken ct)
//    {
//        var tenantId = tenantContext.GetRequiredTenantId();

//        var membership = await db.StaffMemberships.FirstOrDefaultAsync(m => m.Id == membershipId, ct)
//            ?? throw new NotFoundException("StaffMembership", membershipId);

//        if (membership.TenantId != tenantId)
//            throw new CrossTenantAccessException();

//        return membership;
//    }

//    private async Task<Guid> GetRequiredRoleIdAsync(string roleName, CancellationToken ct)
//    {
//        var role = await db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Name == roleName, ct)
//            ?? throw new ConflictException($"Tenant role '{roleName}' is not provisioned. Check database seeding.");
//        return role.Id;
//    }

//    private static bool IsDuplicate(DbUpdateException ex) =>
//        ex.InnerException is not null &&
//        (ex.InnerException.Message.Contains("IX_StaffMemberships", StringComparison.Ordinal) ||
//         ex.InnerException.Message.Contains("UNIQUE KEY", StringComparison.OrdinalIgnoreCase) ||
//         ex.InnerException.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase));

//    private static StaffMemberDto ToDto(StaffMembership m) => new(
//        m.Id, m.UserId, m.User?.Email ?? string.Empty, m.User?.UserName,
//        m.Role?.Name ?? string.Empty, m.Status.ToString(), m.JoinedAt, m.CreatedAt);
//}

///// <summary>
///// Loads User/Role navigation references for an already-tracked membership via
///// EF change-tracker reference metadata (no reflection, no lazy-loading proxies).
///// </summary>
//internal static class StaffMembershipNavigationLoader
//{
//    public static async Task AttachUserAndRoleAsync(
//        this StaffMembership membership, ApplicationDbContext db, CancellationToken ct)
//    {
//        var entry = db.Entry(membership);

//        if (!entry.Reference(m => m.User).IsLoaded)
//            await entry.Reference(m => m.User).LoadAsync(ct);

//        if (!entry.Reference(m => m.Role).IsLoaded)
//            await entry.Reference(m => m.Role).LoadAsync(ct);
//    }
//}
