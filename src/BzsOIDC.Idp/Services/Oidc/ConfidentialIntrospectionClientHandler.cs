using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace BzsOIDC.Idp.Services.Oidc;

internal sealed class ConfidentialIntrospectionClientHandler(IOpenIddictApplicationManager applicationManager)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ValidateIntrospectionRequestContext>
{
    public async ValueTask HandleAsync(OpenIddictServerEvents.ValidateIntrospectionRequestContext context)
    {
        var clientId = context.Request.ClientId;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            context.Reject(
                OpenIddictConstants.Errors.InvalidClient,
                "The introspection endpoint requires a confidential client.",
                null);
            return;
        }

        var application = await applicationManager.FindByClientIdAsync(clientId, context.CancellationToken);
        if (application is null)
        {
            context.Reject(
                OpenIddictConstants.Errors.InvalidClient,
                "The specified introspection client is invalid.",
                null);
            return;
        }

        var clientType = await applicationManager.GetClientTypeAsync(application, context.CancellationToken);
        if (!string.Equals(clientType, OpenIddictConstants.ClientTypes.Confidential, StringComparison.OrdinalIgnoreCase))
        {
            context.Reject(
                OpenIddictConstants.Errors.InvalidClient,
                "Public clients are not allowed to use the introspection endpoint.",
                null);
        }
    }
}
