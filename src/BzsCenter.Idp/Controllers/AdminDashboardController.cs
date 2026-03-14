using BzsCenter.Idp.Services.Admin;
using BzsCenter.Shared.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BzsCenter.Idp.Controllers;

[ApiController]
public sealed class AdminDashboardController(IAdminDashboardService dashboardService) : ControllerBase
{
    [HttpGet("~/api/admin/dashboard/summary")]
    [PermissionAuthorize(PermissionConstants.ClientsRead)]
    public async Task<ActionResult<AdminDashboardSummaryResponse>> GetSummary(CancellationToken cancellationToken)
    {
        var summary = await dashboardService.GetSummaryAsync(cancellationToken);
        return Ok(summary);
    }
}
