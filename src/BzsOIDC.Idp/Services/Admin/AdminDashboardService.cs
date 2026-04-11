using BzsOIDC.Idp.Models;
using BzsOIDC.Idp.Services.Identity;
using BzsOIDC.Idp.Services.Oidc;
using Microsoft.AspNetCore.Identity;

namespace BzsOIDC.Idp.Services.Admin;

public interface IAdminDashboardService
{
    Task<AdminDashboardSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken = default);
}

public sealed class AdminDashboardSummaryResponse
{
    public int TotalUsers { get; init; }
    public int AdminUsers { get; init; }
    public int StandardUsers { get; init; }
    public int TotalClients { get; init; }
    public int InteractiveClients { get; init; }
    public int MachineClients { get; init; }
    public int TotalPermissionMappings { get; init; }
    public int TotalConfiguredScopes { get; init; }
}

internal sealed class AdminDashboardService(
    IUserService userService,
    IOidcClientService clientService,
    IPermissionScopeService permissionScopeService,
    UserManager<BzsUser> userManager) : IAdminDashboardService
{
    public async Task<AdminDashboardSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var users = await userService.GetAllAsync(cancellationToken);
        var clients = await clientService.GetAllAsync(cancellationToken);
        var permissionMappings = await permissionScopeService.GetAllAsync(cancellationToken);

        var adminUsers = 0;
        foreach (var user in users)
        {
            if (await userManager.IsInRoleAsync(user, IdentitySeedConstants.AdminRoleName))
            {
                adminUsers++;
            }
        }

        return new AdminDashboardSummaryResponse
        {
            TotalUsers = users.Count,
            AdminUsers = adminUsers,
            StandardUsers = Math.Max(0, users.Count - adminUsers),
            TotalClients = clients.Count,
            InteractiveClients = clients.Count(static client => client.AuthFlow == OidcClientAuthFlow.AuthorizationCode),
            MachineClients = clients.Count(static client => client.AuthFlow == OidcClientAuthFlow.ClientCredentials),
            TotalPermissionMappings = permissionMappings.Count,
            TotalConfiguredScopes = permissionMappings.Sum(static mapping => mapping.Scopes.Length),
        };
    }
}
