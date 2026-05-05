using BzsOIDC.Idp.Services.Identity;
using BzsOIDC.Shared.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BzsOIDC.Idp.Controllers;

[ApiController]
public sealed class PermissionCatalogController(
    IPermissionCatalogService permissionCatalogService,
    IServiceProvider serviceProvider) : ControllerBase
{
    [HttpGet("~/api/permission-catalog/resources")]
    [PermissionAuthorize(PermissionConstants.PermissionsRead)]
    public async Task<ActionResult<IReadOnlyList<ProtectedResourceResponse>>> GetResources(CancellationToken cancellationToken)
    {
        return Ok(await permissionCatalogService.GetResourcesAsync(cancellationToken));
    }

    [HttpGet("~/api/permission-catalog/resources/{resourceKey}")]
    [PermissionAuthorize(PermissionConstants.PermissionsRead)]
    public async Task<ActionResult<ProtectedResourceResponse>> GetResource(string resourceKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            ModelState.AddModelError(nameof(resourceKey), "Resource/API key is required.");
            return ValidationProblem(ModelState);
        }

        var resource = await permissionCatalogService.GetResourceAsync(resourceKey, cancellationToken);
        return resource is null ? NotFound() : Ok(resource);
    }

    [HttpPut("~/api/permission-catalog/resources/{resourceKey}")]
    [PermissionAuthorize(PermissionConstants.PermissionsWrite)]
    public async Task<ActionResult<ProtectedResourceResponse>> UpsertResource(
        string resourceKey,
        [FromBody] ProtectedResourceUpsertRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            ModelState.AddModelError(nameof(resourceKey), "Resource/API key is required.");
            return ValidationProblem(ModelState);
        }

        var result = await permissionCatalogService.UpsertResourceAsync(resourceKey, request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("~/api/permission-catalog/resources/{resourceKey}/permissions/{permissionName}")]
    [PermissionAuthorize(PermissionConstants.PermissionsWrite)]
    public async Task<ActionResult<PermissionDefinitionResponse>> UpsertPermission(
        string resourceKey,
        string permissionName,
        [FromBody] PermissionDefinitionUpsertRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            ModelState.AddModelError(nameof(resourceKey), "Resource/API key is required.");
        }

        if (string.IsNullOrWhiteSpace(permissionName))
        {
            ModelState.AddModelError(nameof(permissionName), "Permission name is required.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await permissionCatalogService.UpsertPermissionAsync(resourceKey, permissionName, request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("~/api/permission-catalog/permissions/{permissionName}/release-scopes")]
    [PermissionAuthorize(PermissionConstants.PermissionsWrite)]
    public async Task<ActionResult<PermissionDefinitionResponse>> SyncReleaseScopes(
        string permissionName,
        [FromBody] PermissionReleaseScopesUpsertRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(permissionName))
        {
            ModelState.AddModelError(nameof(permissionName), "Permission name is required.");
            return ValidationProblem(ModelState);
        }

        var result = await permissionCatalogService.SyncReleaseScopesAsync(permissionName, request.Scopes, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("~/api/permission-catalog/roles/{roleId:guid}/permissions")]
    [PermissionAuthorize(PermissionConstants.PermissionsRead)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetRolePermissions(Guid roleId, CancellationToken cancellationToken)
    {
        var rolePermissionService = GetRolePermissionService();
        if (rolePermissionService is null)
        {
            return Problem("Role permission service is unavailable.");
        }

        return Ok(await rolePermissionService.GetPermissionsAsync(roleId, cancellationToken));
    }

    [HttpPut("~/api/permission-catalog/roles/{roleId:guid}/permissions")]
    [PermissionAuthorize(PermissionConstants.PermissionsWrite)]
    public async Task<IActionResult> SyncRolePermissions(
        Guid roleId,
        [FromBody] RolePermissionSyncRequest request,
        CancellationToken cancellationToken)
    {
        var rolePermissionService = GetRolePermissionService();
        if (rolePermissionService is null)
        {
            return Problem("Role permission service is unavailable.");
        }

        var result = await rolePermissionService.SyncPermissionsAsync(roleId, request.Permissions, cancellationToken);
        if (result.Succeeded)
        {
            return NoContent();
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(error.Code, error.Description);
        }

        return ValidationProblem(ModelState);
    }

    private IRolePermissionService? GetRolePermissionService()
    {
        return serviceProvider.GetService<IRolePermissionService>();
    }

    private ActionResult<T> ToActionResult<T>(PermissionCatalogCommandResult<T> result)
    {
        return result.Status switch
        {
            PermissionCatalogCommandStatus.Success => Ok(result.Value),
            PermissionCatalogCommandStatus.NotFound => NotFound(),
            PermissionCatalogCommandStatus.Conflict => Conflict(result.Errors.FirstOrDefault()),
            PermissionCatalogCommandStatus.ValidationFailed => ValidationProblem(CreateValidationProblem(result.Errors)),
            _ => Problem("Unexpected permission catalog command status."),
        };
    }

    private ValidationProblemDetails CreateValidationProblem(IEnumerable<string> errors)
    {
        foreach (var error in errors)
        {
            ModelState.AddModelError(nameof(PermissionCatalogController), error);
        }

        return new ValidationProblemDetails(ModelState);
    }
}

public sealed record RolePermissionSyncRequest
{
    public string[] Permissions { get; init; } = [];
}
