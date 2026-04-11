using BzsOIDC.Idp.Services.Identity;
using BzsOIDC.Shared.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BzsOIDC.Idp.Controllers;

[ApiController]
public sealed class PermissionScopesController(IPermissionScopeService permissionScopeService) : ControllerBase
{
    /// <summary>
    /// 获取数据。
    /// </summary>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    [HttpGet("~/api/permissions/scopes")]
    [PermissionAuthorize(PermissionConstants.RolesRead)]
    public async Task<ActionResult<IReadOnlyList<PermissionScopeResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var items = await permissionScopeService.GetAllAsync(cancellationToken);
        return Ok(items);
    }

    /// <summary>
    /// 获取数据。
    /// </summary>
    /// <param name="permission">参数permission。</param>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    [HttpGet("~/api/permissions/scopes/{permission}")]
    [PermissionAuthorize(PermissionConstants.RolesRead)]
    public async Task<ActionResult<PermissionScopeResponse>> GetByPermission(string permission,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(permission))
        {
            ModelState.AddModelError(nameof(permission), "Permission is required.");
            return ValidationProblem(ModelState);
        }

        var result = await permissionScopeService.GetByPermissionAsync(permission, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// 执行Upsert。
    /// </summary>
    /// <param name="permission">参数permission。</param>
    /// <param name="request">参数request。</param>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    [HttpPut("~/api/permissions/scopes/{permission}")]
    [PermissionAuthorize(PermissionConstants.RolesWrite)]
    public async Task<ActionResult<PermissionScopeResponse>> Upsert(
        string permission,
        [FromBody] PermissionScopeUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var errors = Validate(permission, request);
        if (errors.Count > 0)
        {
            foreach (var (key, values) in errors)
            {
                foreach (var value in values)
                {
                    ModelState.AddModelError(key, value);
                }
            }

            return ValidationProblem(ModelState);
        }

        await permissionScopeService.UpsertAsync(permission, request.Scopes, cancellationToken);
        var result = await permissionScopeService.GetByPermissionAsync(permission, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// 删除数据。
    /// </summary>
    /// <param name="permission">参数permission。</param>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    [HttpDelete("~/api/permissions/scopes/{permission}")]
    [PermissionAuthorize(PermissionConstants.RolesWrite)]
    public async Task<IActionResult> Delete(string permission, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(permission))
        {
            ModelState.AddModelError(nameof(permission), "Permission is required.");
            return ValidationProblem(ModelState);
        }

        var deleted = await permissionScopeService.DeleteAsync(permission, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>
    /// 校验输入。
    /// </summary>
    /// <param name="permission">参数permission。</param>
    /// <param name="request">参数request。</param>
    /// <returns>执行结果。</returns>
    private static Dictionary<string, string[]> Validate(string permission, PermissionScopeUpsertRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(permission))
        {
            errors[nameof(permission)] = ["Permission is required."];
        }

        if (request.Scopes.Length == 0)
        {
            errors[nameof(request.Scopes)] = ["At least one scope is required."];
            return errors;
        }

        var invalid = request.Scopes
            .Where(static scope => string.IsNullOrWhiteSpace(scope))
            .ToArray();

        if (invalid.Length > 0)
        {
            errors[nameof(request.Scopes)] = ["Scopes cannot contain empty value."];
        }

        return errors;
    }
}
