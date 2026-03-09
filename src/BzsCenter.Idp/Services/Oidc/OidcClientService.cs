using OpenIddict.Abstractions;

namespace BzsCenter.Idp.Services.Oidc;

public interface IOidcClientService
{
    Task<OidcClientCommandResult<OidcClientRegistrationResponse>> RegisterAsync(
        OidcClientUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OidcClientResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<OidcClientResponse?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default);

    Task<OidcClientCommandResult<OidcClientResponse>> UpdateAsync(
        string clientId,
        OidcClientUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string clientId, CancellationToken cancellationToken = default);
}

internal sealed class OidcClientService(IOpenIddictApplicationManager applicationManager) : IOidcClientService
{
    /// <summary>
    /// 创建数据。
    /// </summary>
    /// <param name="request">参数request。</param>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    public async Task<OidcClientCommandResult<OidcClientRegistrationResponse>> RegisterAsync(
        OidcClientUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = OidcClientDescriptorFactory.ValidateRequest(request);
        if (errors.Count > 0)
        {
            return new OidcClientCommandResult<OidcClientRegistrationResponse>
            {
                Status = OidcClientCommandStatus.ValidationFailed,
                Errors = errors.ToArray(),
            };
        }

        var clientId = string.IsNullOrWhiteSpace(request.ClientId)
            ? $"client-{Guid.NewGuid():N}"
            : request.ClientId.Trim();

        var exists = await applicationManager.FindByClientIdAsync(clientId, cancellationToken);
        if (exists is not null)
        {
            return new OidcClientCommandResult<OidcClientRegistrationResponse>
            {
                Status = OidcClientCommandStatus.Conflict,
                Errors = [$"Client '{clientId}' already exists."],
            };
        }

        var descriptor = OidcClientDescriptorFactory.CreateDescriptor(request, clientId);
        await applicationManager.CreateAsync(descriptor, cancellationToken);

        return new OidcClientCommandResult<OidcClientRegistrationResponse>
        {
            Status = OidcClientCommandStatus.Success,
            Value = new OidcClientRegistrationResponse
            {
                ClientId = descriptor.ClientId!,
                ClientSecret = descriptor.ClientSecret,
                DisplayName = descriptor.DisplayName ?? descriptor.ClientId!,
            },
        };
    }

    /// <summary>
    /// 获取数据。
    /// </summary>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    public async Task<IReadOnlyList<OidcClientResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<OidcClientResponse>();

        await foreach (var application in applicationManager.ListAsync())
        {
            list.Add(await ToResponseAsync(application, cancellationToken));
        }

        return list.OrderBy(static x => x.ClientId, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>
    /// 获取数据。
    /// </summary>
    /// <param name="clientId">参数clientId。</param>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    public async Task<OidcClientResponse?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default)
    {
        var application = await applicationManager.FindByClientIdAsync(clientId, cancellationToken);
        return application is null ? null : await ToResponseAsync(application, cancellationToken);
    }

    /// <summary>
    /// 更新数据。
    /// </summary>
    /// <param name="clientId">参数clientId。</param>
    /// <param name="request">参数request。</param>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    public async Task<OidcClientCommandResult<OidcClientResponse>> UpdateAsync(
        string clientId,
        OidcClientUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = OidcClientDescriptorFactory.ValidateRequest(request);
        if (errors.Count > 0)
        {
            return new OidcClientCommandResult<OidcClientResponse>
            {
                Status = OidcClientCommandStatus.ValidationFailed,
                Errors = errors.ToArray(),
            };
        }

        var application = await applicationManager.FindByClientIdAsync(clientId, cancellationToken);
        if (application is null)
        {
            return new OidcClientCommandResult<OidcClientResponse>
            {
                Status = OidcClientCommandStatus.NotFound,
            };
        }

        var descriptor = OidcClientDescriptorFactory.CreateDescriptor(request, clientId);
        await applicationManager.UpdateAsync(application, descriptor, cancellationToken);

        return new OidcClientCommandResult<OidcClientResponse>
        {
            Status = OidcClientCommandStatus.Success,
            Value = await ToResponseAsync(application, cancellationToken),
        };
    }

    /// <summary>
    /// 删除数据。
    /// </summary>
    /// <param name="clientId">参数clientId。</param>
    /// <param name="cancellationToken">参数cancellationToken。</param>
    /// <returns>执行结果。</returns>
    public async Task<bool> DeleteAsync(string clientId, CancellationToken cancellationToken = default)
    {
        var application = await applicationManager.FindByClientIdAsync(clientId, cancellationToken);
        if (application is null)
        {
            return false;
        }

        await applicationManager.DeleteAsync(application, cancellationToken);
        return true;
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
}
