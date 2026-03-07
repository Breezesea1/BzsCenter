using Microsoft.AspNetCore.Identity;

namespace BzsCenter.Idp.Services.Identity;

public class ChineseIdentityErrorDescriber : IdentityErrorDescriber
{
    private static IdentityError Error(string code, string description)
        => new()
        {
            Code = code,
            Description = description
        };

    public override IdentityError DefaultError()
        => Error(nameof(DefaultError), "发生未知错误，请稍后重试");

    public override IdentityError ConcurrencyFailure()
        => Error(nameof(ConcurrencyFailure), "数据已被其他操作修改，请刷新后重试");

    public override IdentityError PasswordTooShort(int length)
        => Error(nameof(PasswordTooShort), $"密码太短，至少需要 {length} 个字符");

    public override IdentityError PasswordRequiresNonAlphanumeric()
        => Error(nameof(PasswordRequiresNonAlphanumeric), "密码必须包含至少一个非字母数字字符（例如：! @ # $ % 等）");

    public override IdentityError PasswordRequiresDigit()
        => Error(nameof(PasswordRequiresDigit), "密码必须包含至少一个数字");

    public override IdentityError PasswordRequiresLower()
        => Error(nameof(PasswordRequiresLower), "密码必须包含至少一个小写字母");

    public override IdentityError PasswordRequiresUpper()
        => Error(nameof(PasswordRequiresUpper), "密码必须包含至少一个大写字母");

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars)
        => Error(nameof(PasswordRequiresUniqueChars), $"密码至少需要包含 {uniqueChars} 个不同字符");

    public override IdentityError PasswordMismatch()
        => Error(nameof(PasswordMismatch), "密码不正确");

    public override IdentityError DuplicateUserName(string userName)
        => Error(nameof(DuplicateUserName), $"用户名 '{userName}' 已存在");

    public override IdentityError InvalidUserName(string? userName)
        => Error(nameof(InvalidUserName), $"用户名 '{userName}' 无效，只能包含字母、数字及允许的特殊字符");

    public override IdentityError DuplicateEmail(string email)
        => Error(nameof(DuplicateEmail), $"邮箱 '{email}' 已被使用");

    public override IdentityError InvalidEmail(string? email)
        => Error(nameof(InvalidEmail), $"邮箱 '{email}' 格式无效");

    public override IdentityError DuplicateRoleName(string role)
        => Error(nameof(DuplicateRoleName), $"角色 '{role}' 已存在");

    public override IdentityError InvalidRoleName(string? role)
        => Error(nameof(InvalidRoleName), $"角色名 '{role}' 无效");

    public override IdentityError UserAlreadyHasPassword()
        => Error(nameof(UserAlreadyHasPassword), "该用户已设置密码");

    public override IdentityError UserLockoutNotEnabled()
        => Error(nameof(UserLockoutNotEnabled), "该用户未启用锁定功能");

    public override IdentityError UserAlreadyInRole(string role)
        => Error(nameof(UserAlreadyInRole), $"该用户已在角色 '{role}' 中");

    public override IdentityError UserNotInRole(string role)
        => Error(nameof(UserNotInRole), $"该用户不在角色 '{role}' 中");

    public override IdentityError LoginAlreadyAssociated()
        => Error(nameof(LoginAlreadyAssociated), "该外部登录已关联到其他用户");

    public override IdentityError InvalidToken()
        => Error(nameof(InvalidToken), "令牌无效，请重新获取后再试");

    public override IdentityError RecoveryCodeRedemptionFailed()
        => Error(nameof(RecoveryCodeRedemptionFailed), "恢复码无效或已使用");
}
