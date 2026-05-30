using OpenIddict.Abstractions;

namespace BzsOIDC.Idp.Services.Oidc;

internal sealed class OidcClientPermissionBackfillService(IOpenIddictApplicationManager applicationManager)
{
    public async Task EnsureBackfilledAsync(CancellationToken cancellationToken = default)
    {
        var applications = new List<object>();
        await foreach (var application in applicationManager.ListAsync(cancellationToken: cancellationToken))
        {
            applications.Add(application);
        }

        foreach (var application in applications)
        {
            var permissions = (await applicationManager.GetPermissionsAsync(application, cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!TryAddMissingEndpointPermissions(
                    permissions,
                    await applicationManager.GetClientTypeAsync(application, cancellationToken)))
            {
                continue;
            }

            var descriptor = new OpenIddictApplicationDescriptor();
            await applicationManager.PopulateAsync(descriptor, application, cancellationToken);
            descriptor.Permissions.Clear();
            descriptor.Permissions.UnionWith(permissions);

            await applicationManager.UpdateAsync(application, descriptor, cancellationToken);
        }
    }

    private static bool TryAddMissingEndpointPermissions(HashSet<string> permissions, string? clientType)
    {
        var changed = false;
        var grantTypes = permissions
            .Where(static permission => permission.StartsWith(
                OpenIddictConstants.Permissions.Prefixes.GrantType,
                StringComparison.OrdinalIgnoreCase))
            .Select(static permission => permission[OpenIddictConstants.Permissions.Prefixes.GrantType.Length..])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (grantTypes.Contains(OpenIddictConstants.GrantTypes.RefreshToken))
        {
            changed |= permissions.Add(OpenIddictConstants.Permissions.Endpoints.Revocation);
        }

        if (string.Equals(clientType, OpenIddictConstants.ClientTypes.Confidential, StringComparison.OrdinalIgnoreCase) &&
            grantTypes.Contains(OpenIddictConstants.GrantTypes.ClientCredentials))
        {
            changed |= permissions.Add(OpenIddictConstants.Permissions.Endpoints.Introspection);
        }

        return changed;
    }
}
