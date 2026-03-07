using BzsCenter.Idp.Services.Oidc;
using BzsCenter.Shared.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;

namespace BzsCenter.Idp.Controllers;

[ApiController]
public sealed class OidcClientsController(IOpenIddictApplicationManager applicationManager) : ControllerBase
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
        var validationResult = Validate(request);
        if (validationResult is not null)
        {
            return validationResult;
        }

        var clientId = string.IsNullOrWhiteSpace(request.ClientId)
            ? $"client-{Guid.NewGuid():N}"
            : request.ClientId.Trim();

        var exists = await applicationManager.FindByClientIdAsync(clientId, cancellationToken);
        if (exists is not null)
        {
            return Conflict($"Client '{clientId}' already exists.");
        }

        var descriptor = OidcClientDescriptorFactory.CreateDescriptor(request, clientId);
        await applicationManager.CreateAsync(descriptor, cancellationToken);

        return CreatedAtAction(nameof(GetByClientId), new { clientId }, new OidcClientRegistrationResponse
        {
            ClientId = descriptor.ClientId!,
            ClientSecret = descriptor.ClientSecret,
            DisplayName = descriptor.DisplayName ?? descriptor.ClientId!,
        });
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
        var application = await applicationManager.FindByClientIdAsync(clientId, cancellationToken);
        if (application is null)
        {
            return NotFound();
        }

        await applicationManager.DeleteAsync(application, cancellationToken);
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
        var list = new List<OidcClientResponse>();

        await foreach (var application in applicationManager.ListAsync())
        {
            list.Add(await ToResponseAsync(application, cancellationToken));
        }

        return Ok(list.OrderBy(static x => x.ClientId, StringComparer.OrdinalIgnoreCase));
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
        var application = await applicationManager.FindByClientIdAsync(clientId, cancellationToken);
        if (application is null)
        {
            return NotFound();
        }

        return Ok(await ToResponseAsync(application, cancellationToken));
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
        var validationResult = Validate(request);
        if (validationResult is not null)
        {
            return validationResult;
        }

        var application = await applicationManager.FindByClientIdAsync(clientId, cancellationToken);
        if (application is null)
        {
            return NotFound();
        }

        var descriptor = OidcClientDescriptorFactory.CreateDescriptor(request, clientId);
        await applicationManager.UpdateAsync(application, descriptor, cancellationToken);

        return Ok(await ToResponseAsync(application, cancellationToken));
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
    /// 执行ToResponseAsync。
    /// </summary>
    /// <param name="application">参数application。</param>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    private async Task<OidcClientResponse> ToResponseAsync(object application, CancellationToken cancellationToken)
    {
        var permissions = (await applicationManager.GetPermissionsAsync(application, cancellationToken)).ToArray();
        var requirements = (await applicationManager.GetRequirementsAsync(application, cancellationToken)).ToArray();
        var redirectUris = (await applicationManager.GetRedirectUrisAsync(application, cancellationToken))
            .Select(static uri => uri)
            .ToArray();
        var postLogoutRedirectUris = (await applicationManager.GetPostLogoutRedirectUrisAsync(application, cancellationToken))
            .Select(static uri => uri)
            .ToArray();

        return new OidcClientResponse
        {
            ClientId = await applicationManager.GetClientIdAsync(application, cancellationToken) ?? string.Empty,
            DisplayName = await applicationManager.GetDisplayNameAsync(application, cancellationToken),
            PublicClient = string.Equals(
                await applicationManager.GetClientTypeAsync(application, cancellationToken),
                OpenIddictConstants.ClientTypes.Public,
                StringComparison.OrdinalIgnoreCase),
            GrantTypes = permissions
                .Where(static permission => permission.StartsWith(OpenIddictConstants.Permissions.Prefixes.GrantType,
                    StringComparison.OrdinalIgnoreCase))
                .Select(static permission => permission[OpenIddictConstants.Permissions.Prefixes.GrantType.Length..])
                .ToArray(),
            Scopes = permissions
                .Where(static permission => permission.StartsWith(OpenIddictConstants.Permissions.Prefixes.Scope,
                    StringComparison.OrdinalIgnoreCase))
                .Select(static permission => permission[OpenIddictConstants.Permissions.Prefixes.Scope.Length..])
                .ToArray(),
            RedirectUris = redirectUris,
            PostLogoutRedirectUris = postLogoutRedirectUris,
            Permissions = permissions,
            Requirements = requirements,
        };
    }

    /// <summary>
    /// 校验输入。
    /// </summary>
    /// <param name="request">参数request。</param>
    /// <returns>执行结果。</returns>
    private ActionResult? Validate(OidcClientUpsertRequest request)
    {
        var errors = OidcClientDescriptorFactory.ValidateRequest(request);
        if (errors.Count == 0)
        {
            return null;
        }

        foreach (var error in errors)
        {
            ModelState.AddModelError(nameof(request), error);
        }

        return ValidationProblem(ModelState);
    }
}
