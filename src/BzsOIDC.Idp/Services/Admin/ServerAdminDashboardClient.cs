using BzsOIDC.Idp.Client.Services.Dashboard;
using BzsOIDC.Shared.Infrastructure.Authorization;
using Microsoft.AspNetCore.Components.Authorization;

namespace BzsOIDC.Idp.Services.Admin;

internal sealed class ServerAdminDashboardClient(
    IAdminDashboardService dashboardService,
    AuthenticationStateProvider authenticationStateProvider) : IAdminDashboardClient
{
    public async Task<AdminDashboardSummaryResult> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var authenticationState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = authenticationState.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            return AdminDashboardSummaryResult.RequiresLogin();
        }

        var hasClientsReadPermission = user.Claims.Any(claim =>
            string.Equals(claim.Type, PermissionConstants.ClaimType, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(claim.Value, PermissionConstants.ClientsRead, StringComparison.OrdinalIgnoreCase));

        if (!hasClientsReadPermission)
        {
            return AdminDashboardSummaryResult.AccessDenied();
        }

        var summary = await dashboardService.GetSummaryAsync(cancellationToken);
        return AdminDashboardSummaryResult.Success(new AdminDashboardSummaryModel
        {
            TotalUsers = summary.TotalUsers,
            AdminUsers = summary.AdminUsers,
            StandardUsers = summary.StandardUsers,
            TotalClients = summary.TotalClients,
            InteractiveClients = summary.InteractiveClients,
            MachineClients = summary.MachineClients,
            TotalPermissionMappings = summary.TotalPermissionMappings,
            TotalConfiguredScopes = summary.TotalConfiguredScopes
        });
    }
}
