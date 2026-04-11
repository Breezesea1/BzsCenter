using BzsOIDC.Idp.Services.Oidc;
using BzsOIDC.Shared.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BzsOIDC.Idp.Controllers;

[ApiController]
public sealed class OidcScopesController(IOidcScopeService oidcScopeService) : ControllerBase
{
    [HttpGet("~/api/oidc/scopes")]
    [PermissionAuthorize(PermissionConstants.ScopesRead)]
    public async Task<ActionResult<IReadOnlyList<OidcScopeResponse>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await oidcScopeService.GetAllAsync(cancellationToken));
    }

    [HttpGet("~/api/oidc/scopes/{name}")]
    [PermissionAuthorize(PermissionConstants.ScopesRead)]
    public async Task<ActionResult<OidcScopeResponse>> GetByName(string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError(nameof(name), "Scope name is required.");
            return ValidationProblem(ModelState);
        }

        var scope = await oidcScopeService.GetByNameAsync(name, cancellationToken);
        return scope is null ? NotFound() : Ok(scope);
    }

    [HttpPost("~/api/oidc/scopes")]
    [PermissionAuthorize(PermissionConstants.ScopesWrite)]
    public async Task<ActionResult<OidcScopeResponse>> Create([FromBody] OidcScopeUpsertRequest request, CancellationToken cancellationToken)
    {
        var result = await oidcScopeService.RegisterAsync(request, cancellationToken);
        if (result.Status == OidcScopeCommandStatus.ValidationFailed)
        {
            return ValidationProblem(CreateValidationProblem(result.Errors));
        }

        if (result.Status == OidcScopeCommandStatus.Conflict)
        {
            return Conflict(result.Errors[0]);
        }

        var payload = result.Value!;
        return CreatedAtAction(nameof(GetByName), new { name = payload.Name }, payload);
    }

    [HttpPut("~/api/oidc/scopes/{name}")]
    [PermissionAuthorize(PermissionConstants.ScopesWrite)]
    public async Task<ActionResult<OidcScopeResponse>> Update(
        string name,
        [FromBody] OidcScopeUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var result = await oidcScopeService.UpdateAsync(name, request, cancellationToken);
        if (result.Status == OidcScopeCommandStatus.ValidationFailed)
        {
            return ValidationProblem(CreateValidationProblem(result.Errors));
        }

        if (result.Status == OidcScopeCommandStatus.NotFound)
        {
            return NotFound();
        }

        return Ok(result.Value);
    }

    [HttpDelete("~/api/oidc/scopes/{name}")]
    [PermissionAuthorize(PermissionConstants.ScopesWrite)]
    public async Task<IActionResult> Delete(string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError(nameof(name), "Scope name is required.");
            return ValidationProblem(ModelState);
        }

        var deleted = await oidcScopeService.DeleteAsync(name, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    private ValidationProblemDetails CreateValidationProblem(IEnumerable<string> errors)
    {
        foreach (var error in errors)
        {
            ModelState.AddModelError(nameof(OidcScopeUpsertRequest), error);
        }

        return new ValidationProblemDetails(ModelState);
    }
}
