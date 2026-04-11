using System.Net.Http.Json;

namespace BzsOIDC.Idp.Client.Services.Dashboard;

public interface IAdminDashboardClient
{
    Task<AdminDashboardSummaryModel?> GetSummaryAsync(CancellationToken cancellationToken = default);
}

public sealed class AdminDashboardSummaryModel
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

internal sealed class AdminDashboardClient(HttpClient httpClient) : IAdminDashboardClient
{
    public async Task<AdminDashboardSummaryModel?> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("api/admin/dashboard/summary", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(mediaType, "text/json", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<AdminDashboardSummaryModel>(cancellationToken: cancellationToken);
    }
}
