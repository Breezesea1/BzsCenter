using Microsoft.AspNetCore.Identity;

namespace BzsCenter.Idp.Services.Identity;

public class ChineseIdentityErrorDescriber : IdentityErrorDescriber
{
    /// <summary>
    /// 执行Error。
    /// </summary>
    /// <param name="code">参数code。</param>
    /// <param name="description">参数description。</param>
    /// <returns>执行结果。</returns>
    private static IdentityError Error(string code, string description)
        => new()
        {
            Code = code,
            Description = description
        };

    /// <summary>
    /// 执行DefaultError。
    /// </summary>
    /// <returns>执行结果。</returns>
    public override IdentityError DefaultError()
        => Error(nameof(DefaultError), "发生未知错误，请稍后重试");

    /// <summary>
    /// 执行ConcurrencyFailure。
    /// </summary>
    /// <returns>执行结果。</returns>
    public override IdentityError ConcurrencyFailure()
        => Error(nameof(ConcurrencyFailure), "数据已被其他操作修改，请刷新后重试");

    /// <summary>
    /// 执行PasswordTooShort。
    /// </summary>
    /// <param name="length">参数length。</param>
    /// <returns>执行结果。</returns>
    public override IdentityError PasswordTooShort(int length)
        => Error(nameof(PasswordTooShort), $"密码太短，至少需要 {length} 个字符");

    /// <summary>
    /// 执行PasswordRequiresNonAlphanumeric。
    /// </summary>
    /// <returns>执行结果。</returns>
    public override IdentityError PasswordRequiresNonAlphanumeric()
        => Error(nameof(PasswordRequiresNonAlphanumeric), "密码必须包含至少一个非字母数字字符（例如：! @ # $ % 等）");

    /// <summary>
    /// 执行PasswordRequiresDigit。
    /// </summary>
    /// <returns>执行结果。</returns>
    public override IdentityError PasswordRequiresDigit()
        => Error(nameof(PasswordRequiresDigit), "密码必须包含至少一个数字");

    /// <summary>
    /// 执行PasswordRequiresLower。
    /// </summary>
    /// <returns>执行结果。</returns>
    public override IdentityError PasswordRequiresLower()
        => Error(nameof(PasswordRequiresLower), "密码必须包含至少一个小写字母");

    /// <summary>
    /// 执行PasswordRequiresUpper。
    /// </summary>
    /// <returns>执行结果。</returns>
    public override IdentityError PasswordRequiresUpper()
        => Error(nameof(PasswordRequiresUpper), "密码必须包含至少一个大写字母");

    /// <summary>
    /// 执行PasswordRequiresUniqueChars。
    /// </summary>
    /// <param name="uniqueChars">参数uniqueChars。</param>
    /// <returns>执行结果。</returns>
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars)
        => Error(nameof(PasswordRequiresUniqueChars), $"密码至少需要包含 {uniqueChars} 个不同字符");

    /// <summary>
    /// 执行PasswordMismatch。
    /// </summary>
    /// <returns>执行结果。</returns>
    public override IdentityError PasswordMismatch()
        => Error(nameof(PasswordMismatch), "密码不正确");

    /// <summary>
    /// 执行DuplicateUserName。
    /// </summary>
    /// <param name="userName">参数userName。</param>
    /// <returns>执行结果。</returns>
    public override IdentityError DuplicateUserName(string userName)
        => Error(nameof(DuplicateUserName), $"用户名 '{userName}' 已存在");

    /// <summary>
    /// 执行InvalidUserName。
    /// </summary>
    /// <param name="userName">参数userName。</param>
    /// <returns>执行结果。</returns>
    public override IdentityError InvalidUserName(string? userName)
        => Error(nameof(InvalidUserName), $"用户名 '{userName}' 无效，只能包含字母、数字及允许的特殊字符");

    /// <summary>
    /// 执行DuplicateEmail。
    /// </summary>
    /// <param name="email">参数email。</param>
    /// <returns>执行结果。</returns>
    public override IdentityError DuplicateEmail(string email)
        => Error(nameof(DuplicateEmail), $"邮箱 '{email}' 已被使用");

    /// <summary>
    /// 执行InvalidEmail。
    /// </summary>
    /// <param name="email">参数email。</param>
    /// <returns>执行结果。</returns>
    public override IdentityError InvalidEmail(string? email)
        => Error(nameof(InvalidEmail), $"邮箱 '{email}' 格式无效");

    /// <summary>
    /// 执行DuplicateRoleName。
    /// </summary>
    /// <param name="role">参数role。</param>
    /// <returns>执行结果。</returns>
    public override IdentityError DuplicateRoleName(string role)
        => Error(nameof(DuplicateRoleName), $"角色 '{role}' 已存在");

    /// <summary>
    /// 执行InvalidRoleName。
    /// </summary>
    /// <param name="role">参数role。</param>
    /// <returns>执行结果。</returns>
    public override IdentityError InvalidRoleName(string? role)
        => Error(nameof(InvalidRoleName), $"角色名 '{role}' 无效");

    /// <summary>
    /// 执行UserAlreadyHasPassword。
    /// </summary>
    /// <returns>执行结果。</returns>
    public override IdentityError UserAlreadyHasPassword()
        => Error(nameof(UserAlreadyHasPassword), "该用户已设置密码");

    /// <summary>
    /// 执行UserLockoutNotEnabled。
    /// </summary>
    /// <returns>执行结果。</returns>
    public override IdentityError UserLockoutNotEnabled()
        => Error(nameof(UserLockoutNotEnabled), "该用户未启用锁定功能");

    /// <summary>
    /// 执行UserAlreadyInRole。
    /// </summary>
    /// <param name="role">参数role。</param>
    /// <returns>执行结果。</returns>
    public override IdentityError UserAlreadyInRole(string role)
        => Error(nameof(UserAlreadyInRole), $"该用户已在角色 '{role}' 中");

    /// <summary>
    /// 执行UserNotInRole。
    /// </summary>
    /// <param name="role">参数role。</param>
    /// <returns>执行结果。</returns>
    public override IdentityError UserNotInRole(string role)
        => Error(nameof(UserNotInRole), $"该用户不在角色 '{role}' 中");

    /// <summary>
    /// 执行LoginAlreadyAssociated。
    /// </summary>
    /// <returns>执行结果。</returns>
    public override IdentityError LoginAlreadyAssociated()
        => Error(nameof(LoginAlreadyAssociated), "该外部登录已关联到其他用户");

    /// <summary>
    /// 执行InvalidToken。
    /// </summary>
    /// <returns>执行结果。</returns>
    public override IdentityError InvalidToken()
        => Error(nameof(InvalidToken), "令牌无效，请重新获取后再试");

    /// <summary>
    /// 执行RecoveryCodeRedemptionFailed。
    /// </summary>
    /// <returns>执行结果。</returns>
    public override IdentityError RecoveryCodeRedemptionFailed()
        => Error(nameof(RecoveryCodeRedemptionFailed), "恢复码无效或已使用");
}
