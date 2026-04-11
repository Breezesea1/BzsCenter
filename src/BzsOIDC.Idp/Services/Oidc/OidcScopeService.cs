using OpenIddict.Abstractions;

namespace BzsOIDC.Idp.Services.Oidc;

public interface IOidcScopeService
{
    Task<OidcScopeCommandResult<OidcScopeResponse>> RegisterAsync(
        OidcScopeUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OidcScopeResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<OidcScopeResponse?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<OidcScopeCommandResult<OidcScopeResponse>> UpdateAsync(
        string name,
        OidcScopeUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string name, CancellationToken cancellationToken = default);
    Task InitializeDefaultsIfMissingAsync(IEnumerable<string> scopeNames, CancellationToken cancellationToken = default);
}

internal sealed class OidcScopeService(IOpenIddictScopeManager scopeManager) : IOidcScopeService
{
    private static readonly string[] ReservedScopeNames =
    [
        OpenIddictConstants.Scopes.OpenId,
        OpenIddictConstants.Scopes.Profile,
        OpenIddictConstants.Scopes.Email,
        OpenIddictConstants.Scopes.Roles,
        OpenIddictConstants.Scopes.OfflineAccess,
    ];

    public async Task<OidcScopeCommandResult<OidcScopeResponse>> RegisterAsync(
        OidcScopeUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidateRequest(request.Name, request);
        if (errors.Count > 0)
        {
            return new OidcScopeCommandResult<OidcScopeResponse>
            {
                Status = OidcScopeCommandStatus.ValidationFailed,
                Errors = errors.ToArray(),
            };
        }

        var normalizedName = NormalizeName(request.Name!);
        var existing = await scopeManager.FindByNameAsync(normalizedName, cancellationToken);
        if (existing is not null)
        {
            return new OidcScopeCommandResult<OidcScopeResponse>
            {
                Status = OidcScopeCommandStatus.Conflict,
                Errors = [$"Scope '{normalizedName}' already exists."],
            };
        }

        await scopeManager.CreateAsync(CreateDescriptor(normalizedName, request), cancellationToken);

        return new OidcScopeCommandResult<OidcScopeResponse>
        {
            Status = OidcScopeCommandStatus.Success,
            Value = await GetByNameAsync(normalizedName, cancellationToken),
        };
    }

    public async Task<IReadOnlyList<OidcScopeResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<OidcScopeResponse>();

        await foreach (var scope in scopeManager.ListAsync())
        {
            list.Add(await ToResponseAsync(scope, cancellationToken));
        }

        return list.OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<OidcScopeResponse?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var scope = await scopeManager.FindByNameAsync(name.Trim(), cancellationToken);
        return scope is null ? null : await ToResponseAsync(scope, cancellationToken);
    }

    public async Task<OidcScopeCommandResult<OidcScopeResponse>> UpdateAsync(
        string name,
        OidcScopeUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidateRequest(name, request);
        if (errors.Count > 0)
        {
            return new OidcScopeCommandResult<OidcScopeResponse>
            {
                Status = OidcScopeCommandStatus.ValidationFailed,
                Errors = errors.ToArray(),
            };
        }

        var normalizedName = NormalizeName(name);
        var scope = await scopeManager.FindByNameAsync(normalizedName, cancellationToken);
        if (scope is null)
        {
            return new OidcScopeCommandResult<OidcScopeResponse>
            {
                Status = OidcScopeCommandStatus.NotFound,
            };
        }

        await scopeManager.UpdateAsync(scope, CreateDescriptor(normalizedName, request), cancellationToken);

        return new OidcScopeCommandResult<OidcScopeResponse>
        {
            Status = OidcScopeCommandStatus.Success,
            Value = await ToResponseAsync(scope, cancellationToken),
        };
    }

    public async Task<bool> DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var normalizedName = NormalizeName(name);
        if (IsReservedScope(normalizedName))
        {
            return false;
        }

        var scope = await scopeManager.FindByNameAsync(normalizedName, cancellationToken);
        if (scope is null)
        {
            return false;
        }

        await scopeManager.DeleteAsync(scope, cancellationToken);
        return true;
    }

    public async Task InitializeDefaultsIfMissingAsync(IEnumerable<string> scopeNames, CancellationToken cancellationToken = default)
    {
        foreach (var scopeName in scopeNames
                     .Where(static item => !string.IsNullOrWhiteSpace(item))
                     .Select(static item => item.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (IsReservedScope(scopeName))
            {
                continue;
            }

            if (await scopeManager.FindByNameAsync(scopeName, cancellationToken) is not null)
            {
                continue;
            }

            await scopeManager.CreateAsync(new OpenIddictScopeDescriptor
            {
                Name = scopeName,
                DisplayName = scopeName,
                Resources = { scopeName },
            }, cancellationToken);
        }
    }

    private static List<string> ValidateRequest(string? name, OidcScopeUpsertRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add("Scope name is required.");
            return errors;
        }

        var normalizedName = NormalizeName(name);
        if (IsReservedScope(normalizedName))
        {
            errors.Add($"Scope '{normalizedName}' is reserved and cannot be managed here.");
        }

        var resources = NormalizeResources(request.Resources, normalizedName);
        if (resources.Length == 0)
        {
            errors.Add("At least one resource is required.");
        }

        return errors;
    }

    private static OpenIddictScopeDescriptor CreateDescriptor(string normalizedName, OidcScopeUpsertRequest request)
    {
        var descriptor = new OpenIddictScopeDescriptor
        {
            Name = normalizedName,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
        };

        descriptor.Resources.UnionWith(NormalizeResources(request.Resources, normalizedName));
        return descriptor;
    }

    private async Task<OidcScopeResponse> ToResponseAsync(object scope, CancellationToken cancellationToken)
    {
        return new OidcScopeResponse
        {
            Name = await scopeManager.GetNameAsync(scope, cancellationToken) ?? string.Empty,
            DisplayName = await scopeManager.GetDisplayNameAsync(scope, cancellationToken),
            Description = await scopeManager.GetDescriptionAsync(scope, cancellationToken),
            Resources = (await scopeManager.GetResourcesAsync(scope, cancellationToken)).ToArray(),
        };
    }

    private static bool IsReservedScope(string scopeName)
    {
        return ReservedScopeNames.Contains(scopeName, StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return name.Trim();
    }

    private static string[] NormalizeResources(IEnumerable<string> resources, string fallbackName)
    {
        var normalized = resources
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(static item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalized.Length > 0 ? normalized : [fallbackName];
    }
}
