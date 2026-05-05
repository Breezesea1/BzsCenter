using BzsOIDC.Idp.Models;

namespace BzsOIDC.Idp.Services.Identity;

internal sealed class RoleManagementPolicy
{
    public string NormalizeName(string roleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);
        return roleName.Trim();
    }

    public string NormalizeKey(string roleName)
    {
        return NormalizeName(roleName).ToUpperInvariant();
    }

    public bool IsProtectedRole(BzsRole role)
    {
        return IsReservedName(role.Name);
    }

    public bool IsReservedName(string? roleName)
    {
        return string.Equals(roleName, IdentitySeedConstants.AdminRoleName, StringComparison.OrdinalIgnoreCase);
    }

    public string[] ValidateCreate(string roleName, bool reservedRoleExists)
    {
        var errors = ValidateName(roleName);
        if (errors.Length > 0)
        {
            return errors;
        }

        var normalizedName = NormalizeName(roleName);
        if (reservedRoleExists && IsReservedName(normalizedName))
        {
            return ["admin 为保留角色名，不能重复创建"];
        }

        return [];
    }

    public string[] ValidateRename(BzsRole role, string targetName)
    {
        var errors = ValidateName(targetName);
        if (errors.Length > 0)
        {
            return errors;
        }

        var normalizedTargetName = NormalizeName(targetName);
        if (IsProtectedRole(role) && !IsReservedName(normalizedTargetName))
        {
            return ["admin 角色不允许重命名"];
        }

        if (!IsProtectedRole(role) && IsReservedName(normalizedTargetName))
        {
            return ["admin 为保留角色名，不能通过重命名创建"];
        }

        return [];
    }

    public string[] ValidateDelete(BzsRole role)
    {
        return IsProtectedRole(role)
            ? ["admin 角色不允许删除"]
            : [];
    }

    private static string[] ValidateName(string roleName)
    {
        return string.IsNullOrWhiteSpace(roleName)
            ? ["Role name is required."]
            : [];
    }
}
