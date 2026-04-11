using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using BzsOIDC.Idp.Controllers;
using BzsOIDC.Idp.Infra;
using BzsOIDC.Idp.Services.Authorization;
using BzsOIDC.Idp.Services.Identity;
using BzsOIDC.Shared.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BzsOIDC.Idp.IntegrationTests;

public sealed class PermissionScopesApiIntegrationTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    [Fact]
    public async Task GetAll_WithoutAuth_ReturnsUnauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/permissions/scopes");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PermissionScopesCrud_WithValidPermissionClaims_WorksEndToEnd()
    {
        using var upsertRequest = CreateAuthorizedRequest(
            HttpMethod.Put,
            "/api/permissions/scopes/users.write",
            new PermissionScopeUpsertRequest { Scopes = ["api", "internal"] });

        using var upsertResponse = await _client.SendAsync(upsertRequest);
        upsertResponse.EnsureSuccessStatusCode();

        var upsertPayload = await upsertResponse.Content.ReadFromJsonAsync<PermissionScopeResponse>();
        Assert.NotNull(upsertPayload);
        Assert.Equal("users.write", upsertPayload.Permission);
        Assert.Equal(["api", "internal"], upsertPayload.Scopes.OrderBy(static x => x).ToArray());

        using var getRequest = CreateAuthorizedRequest(HttpMethod.Get, "/api/permissions/scopes/users.write");
        using var getResponse = await _client.SendAsync(getRequest);
        getResponse.EnsureSuccessStatusCode();

        var getPayload = await getResponse.Content.ReadFromJsonAsync<PermissionScopeResponse>();
        Assert.NotNull(getPayload);
        Assert.Equal("users.write", getPayload.Permission);

        using var deleteRequest = CreateAuthorizedRequest(HttpMethod.Delete, "/api/permissions/scopes/users.write");
        using var deleteResponse = await _client.SendAsync(deleteRequest);

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
        });

        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorization();
        builder.Services.Configure<PermissionPolicyOptions>(_ => { });
        builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

        builder.Services.AddMemoryCache();
        builder.Services.AddDbContext<IdpDbContext>(options => options.UseSqlite(_connection));
        builder.Services.AddScoped<IPermissionScopeService, PermissionScopeService>();

        builder.Services
            .AddControllers()
            .AddApplicationPart(typeof(PermissionScopesController).Assembly);

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapControllers();

        await _app.StartAsync();

        await using (var scope = _app.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IdpDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
        }

        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string url, object? content = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add(TestAuthHandler.UserHeader, "integration-user");
        request.Headers.Add(TestAuthHandler.PermissionHeader, "roles.read,roles.write");

        if (content is not null)
        {
            request.Content = JsonContent.Create(content);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        return request;
    }
}

internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    internal const string SchemeName = "Test";
    internal const string UserHeader = "X-Test-User";
    internal const string PermissionHeader = "X-Test-Permissions";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserHeader, out var userValues) ||
            string.IsNullOrWhiteSpace(userValues.ToString()))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing test user header."));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, userValues.ToString()),
        };

        if (Request.Headers.TryGetValue(PermissionHeader, out var permissionValues))
        {
            var permissions = permissionValues
                .ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            claims.AddRange(permissions.Select(static permission =>
                new Claim(PermissionConstants.ClaimType, permission)));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
