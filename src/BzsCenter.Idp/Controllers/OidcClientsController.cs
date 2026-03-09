using BzsCenter.Idp.Services.Oidc;
using BzsCenter.Shared.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BzsCenter.Idp.Controllers;

[ApiController]
public sealed class OidcClientsController(IOidcClientService oidcClientService) : ControllerBase
{
    /// <summary>
    /// 注册资源。
    /// </summary>
    /// <param name="request">参数request。</param>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    [HttpPost("~/connect/register")]
    [PermissionAuthorize(PermissionConstants.ClientsWrite)]
    public async Task<ActionResult<OidcClientRegistrationResponse>> Register(
        [FromBody] OidcClientUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var result = await oidcClientService.RegisterAsync(request, cancellationToken);
        if (result.Status == OidcClientCommandStatus.ValidationFailed)
        {
            return ValidationProblem(CreateValidationProblem(result.Errors));
        }

        if (result.Status == OidcClientCommandStatus.Conflict)
        {
            return Conflict(result.Errors[0]);
        }

        var payload = result.Value!;
        return CreatedAtAction(nameof(GetByClientId), new { clientId = payload.ClientId }, payload);
    }

    /// <summary>
    /// 注销资源。
    /// </summary>
    /// <param name="clientId">参数clientId。</param>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    [HttpDelete("~/connect/register/{clientId}")]
    [PermissionAuthorize(PermissionConstants.ClientsWrite)]
    public async Task<IActionResult> Unregister(string clientId, CancellationToken cancellationToken)
    {
        var deleted = await oidcClientService.DeleteAsync(clientId, cancellationToken);
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    /// <summary>
    /// 获取数据。
    /// </summary>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    [HttpGet("~/api/oidc/clients")]
    [PermissionAuthorize(PermissionConstants.ClientsRead)]
    public async Task<ActionResult<IReadOnlyList<OidcClientResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var list = await oidcClientService.GetAllAsync(cancellationToken);
        return Ok(list);
    }

    /// <summary>
    /// 获取数据。
    /// </summary>
    /// <param name="clientId">参数clientId。</param>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    [HttpGet("~/api/oidc/clients/{clientId}")]
    [PermissionAuthorize(PermissionConstants.ClientsRead)]
    public async Task<ActionResult<OidcClientResponse>> GetByClientId(string clientId, CancellationToken cancellationToken)
    {
        var application = await oidcClientService.GetByClientIdAsync(clientId, cancellationToken);
        if (application is null)
        {
            return NotFound();
        }

        return Ok(application);
    }

    /// <summary>
    /// 创建数据。
    /// </summary>
    /// <param name="request">参数request。</param>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    [HttpPost("~/api/oidc/clients")]
    [PermissionAuthorize(PermissionConstants.ClientsWrite)]
    public Task<ActionResult<OidcClientRegistrationResponse>> Create(
        [FromBody] OidcClientUpsertRequest request,
        CancellationToken cancellationToken)
    {
        return Register(request, cancellationToken);
    }

    /// <summary>
    /// 更新数据。
    /// </summary>
    /// <param name="clientId">参数clientId。</param>
    /// <param name="request">参数request。</param>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    [HttpPut("~/api/oidc/clients/{clientId}")]
    [PermissionAuthorize(PermissionConstants.ClientsWrite)]
    public async Task<ActionResult<OidcClientResponse>> Update(
        string clientId,
        [FromBody] OidcClientUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var result = await oidcClientService.UpdateAsync(clientId, request, cancellationToken);
        if (result.Status == OidcClientCommandStatus.ValidationFailed)
        {
            return ValidationProblem(CreateValidationProblem(result.Errors));
        }

        if (result.Status == OidcClientCommandStatus.NotFound)
        {
            return NotFound();
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// 删除数据。
    /// </summary>
    /// <param name="clientId">参数clientId。</param>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    [HttpDelete("~/api/oidc/clients/{clientId}")]
    [PermissionAuthorize(PermissionConstants.ClientsWrite)]
    public Task<IActionResult> Delete(string clientId, CancellationToken cancellationToken)
    {
        return Unregister(clientId, cancellationToken);
    }

    /// <summary>
    /// 创建数据。
    /// </summary>
    /// <param name="errors">参数errors。</param>
    /// <returns>执行结果。</returns>
    private ValidationProblemDetails CreateValidationProblem(IEnumerable<string> errors)
    {
        foreach (var error in errors)
        {
            ModelState.AddModelError(nameof(OidcClientUpsertRequest), error);
        }

        return new ValidationProblemDetails(ModelState);
    }
}
