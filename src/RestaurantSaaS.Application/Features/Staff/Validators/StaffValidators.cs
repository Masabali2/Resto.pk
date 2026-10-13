using FluentValidation;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Features.Staff.DTOs;

namespace RestaurantSaaS.Application.Features.Staff.Validators;

internal static class StaffRoleRules
{
    /// <summary>Only tenant-level roles may be assigned through staff management.</summary>
    public static readonly string[] AssignableTenantRoles =
    [
        Permissions.TenantRoles.Owner,
        Permissions.TenantRoles.Manager,
        Permissions.TenantRoles.Kitchen,
        Permissions.TenantRoles.Cashier,
        Permissions.TenantRoles.Waiter
    ];
}

public sealed class InviteStaffRequestValidator : AbstractValidator<InviteStaffRequest>
{
    public InviteStaffRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.RoleName)
            .Must(r => StaffRoleRules.AssignableTenantRoles.Contains(r))
            .WithMessage($"RoleName must be one of: {string.Join(", ", StaffRoleRules.AssignableTenantRoles)}.");
    }
}

public sealed class AssignStaffRoleRequestValidator : AbstractValidator<AssignStaffRoleRequest>
{
    public AssignStaffRoleRequestValidator()
    {
        RuleFor(x => x.RoleName)
            .Must(r => StaffRoleRules.AssignableTenantRoles.Contains(r))
            .WithMessage($"RoleName must be one of: {string.Join(", ", StaffRoleRules.AssignableTenantRoles)}.");
    }
}

public sealed class UpdateStaffStatusRequestValidator : AbstractValidator<UpdateStaffStatusRequest>
{
    private static readonly string[] Allowed = ["Active", "Suspended", "Removed"];

    public UpdateStaffStatusRequestValidator()
    {
        RuleFor(x => x.Status).Must(s => Allowed.Contains(s))
            .WithMessage($"Status must be one of: {string.Join(", ", Allowed)}.");
    }
}
