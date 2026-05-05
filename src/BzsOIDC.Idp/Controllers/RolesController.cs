using BzsOIDC.Idp.Services.Identity;
using BzsOIDC.Shared.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BzsOIDC.Idp.Controllers;

[ApiController]
public sealed class RolesController(IRoleManagementService roleManagementService) : ControllerBase
{
    [HttpGet("~/api/roles")]
    [PermissionAuthorize(PermissionConstants.RolesRead)]
    public async Task<ActionResult<IReadOnlyList<RoleResponse>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await roleManagementService.GetAllAsync(cancellationToken));
    }

    [HttpGet("~/api/roles/{roleId:guid}")]
    [PermissionAuthorize(PermissionConstants.RolesRead)]
    public async Task<ActionResult<RoleResponse>> GetById(Guid roleId, CancellationToken cancellationToken)
    {
        var role = await roleManagementService.GetByIdAsync(roleId, cancellationToken);
        return role is null ? NotFound() : Ok(role);
    }

    [HttpPost("~/api/roles")]
    [PermissionAuthorize(PermissionConstants.RolesWrite)]
    public async Task<ActionResult<RoleResponse>> Create(
        [FromBody] RoleUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var result = await roleManagementService.CreateAsync(request, cancellationToken);
        if (result.Status == RoleManagementCommandStatus.Success)
        {
            var payload = result.Value!;
            return CreatedAtAction(nameof(GetById), new { roleId = payload.Id }, payload);
        }

        return ToActionResult(result);
    }

    [HttpPut("~/api/roles/{roleId:guid}")]
    [PermissionAuthorize(PermissionConstants.RolesWrite)]
    public async Task<ActionResult<RoleResponse>> Update(
        Guid roleId,
        [FromBody] RoleUpsertRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await roleManagementService.UpdateAsync(roleId, request, cancellationToken));
    }

    [HttpDelete("~/api/roles/{roleId:guid}")]
    [PermissionAuthorize(PermissionConstants.RolesWrite)]
    public async Task<IActionResult> Delete(Guid roleId, CancellationToken cancellationToken)
    {
        var result = await roleManagementService.DeleteAsync(roleId, cancellationToken);
        return result.Status == RoleManagementCommandStatus.Success
            ? NoContent()
            : ToFailureActionResult(result);
    }

    [HttpGet("~/api/roles/{roleId:guid}/permissions")]
    [PermissionAuthorize(PermissionConstants.RolesRead)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetPermissions(Guid roleId, CancellationToken cancellationToken)
    {
        var permissions = await roleManagementService.GetPermissionsAsync(roleId, cancellationToken);
        return permissions is null ? NotFound() : Ok(permissions);
    }

    [HttpPut("~/api/roles/{roleId:guid}/permissions")]
    [PermissionAuthorize(PermissionConstants.RolesWrite)]
    public async Task<IActionResult> SyncPermissions(
        Guid roleId,
        [FromBody] RolePermissionSyncRequest request,
        CancellationToken cancellationToken)
    {
        var result = await roleManagementService.SyncPermissionsAsync(roleId, request.Permissions, cancellationToken);
        return result.Status == RoleManagementCommandStatus.Success
            ? NoContent()
            : ToFailureActionResult(result);
    }

    private ActionResult<T> ToActionResult<T>(RoleManagementCommandResult<T> result)
    {
        return result.Status switch
        {
            RoleManagementCommandStatus.Success => Ok(result.Value),
            RoleManagementCommandStatus.NotFound => NotFound(),
            RoleManagementCommandStatus.Conflict => Conflict(result.Errors.FirstOrDefault()),
            RoleManagementCommandStatus.Protected => Conflict(result.Errors.FirstOrDefault()),
            RoleManagementCommandStatus.ValidationFailed => ValidationProblem(CreateValidationProblem(result.Errors)),
            _ => Problem("Unexpected role management command status."),
        };
    }

    private IActionResult ToFailureActionResult<T>(RoleManagementCommandResult<T> result)
    {
        return result.Status switch
        {
            RoleManagementCommandStatus.NotFound => NotFound(),
            RoleManagementCommandStatus.Conflict => Conflict(result.Errors.FirstOrDefault()),
            RoleManagementCommandStatus.Protected => Conflict(result.Errors.FirstOrDefault()),
            RoleManagementCommandStatus.ValidationFailed => ValidationProblem(CreateValidationProblem(result.Errors)),
            _ => Problem("Unexpected role management command status."),
        };
    }

    private ValidationProblemDetails CreateValidationProblem(IEnumerable<string> errors)
    {
        foreach (var error in errors)
        {
            ModelState.AddModelError(nameof(RoleUpsertRequest), error);
        }

        return new ValidationProblemDetails(ModelState);
    }
}
