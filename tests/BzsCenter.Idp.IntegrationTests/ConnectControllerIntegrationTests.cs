using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using BzsCenter.Idp.Controllers;
using BzsCenter.Idp.Domain;
using BzsCenter.Idp.Infra;
using BzsCenter.Idp.Services;
using BzsCenter.Idp.Services.Authorization;
using BzsCenter.Idp.Services.Identity;
using BzsCenter.Idp.Services.Oidc;
using BzsCenter.Shared.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using OpenIddict.Abstractions;

namespace BzsCenter.Idp.IntegrationTests;

public sealed class ConnectControllerIntegrationTests : IAsyncLifetime
{
    private const string MachineClientId = "machine-client";
    private const string MachineClientSecret = "machine-client-secret";
    private const string WebClientId = "web-client";
    private static readonly Uri BaseUri = new("https://localhost");
    private static readonly Uri WebRedirectUri = new("https://localhost/callback");

    private SqliteConnection _connection = null!;
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private string? _authCookieHeader;

    [Fact]
    public async Task Authorize_WhenUnauthenticated_ReturnsUnauthorized()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/connect/authorize?client_id=web-client&response_type=code&scope=openid%20profile&redirect_uri=https%3A%2F%2Flocalhost%2Fcallback&code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM&code_challenge_method=S256");
        request.Headers.Accept.ParseAdd("text/html");

        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var location = response.Headers.Location?.OriginalString;
        Assert.False(string.IsNullOrWhiteSpace(location));
        Assert.Contains("/login", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ReturnUrl=", location, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Exchange_WhenGrantTypeUnsupported_ReturnsOpenIddictErrorPayload()
    {
        using var response = await _client.PostAsync(
            "/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = OpenIddictConstants.GrantTypes.Password,
                ["client_id"] = MachineClientId,
                ["client_secret"] = MachineClientSecret,
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(payload);
        Assert.Equal(OpenIddictConstants.Errors.UnsupportedGrantType, payload.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task UserInfo_WithoutAccessToken_ReturnsUnauthorized()
    {
        using var response = await _client.GetAsync("/connect/userinfo");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Exchange_WhenClientCredentialsValid_ReturnsAccessTokenResponse()
    {
        using var response = await _client.PostAsync(
            "/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = OpenIddictConstants.GrantTypes.ClientCredentials,
                ["client_id"] = MachineClientId,
                ["client_secret"] = MachineClientSecret,
                ["scope"] = PermissionConstants.ScopeApi,
            }));

        response.EnsureSuccessStatusCode();

        using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.RootElement.GetProperty("access_token").GetString()));
        Assert.Equal("Bearer", payload.RootElement.GetProperty("token_type").GetString());
    }

    [Fact]
    public async Task AuthorizationCodeFlow_WhenUserAuthenticated_ReturnsCodeThenTokens()
    {
        await SignInAsAdminAsync();

        var code = await RequestAuthorizationCodeAsync();
        using var tokenResponse = await ExchangeAuthorizationCodeAsync(code);

        tokenResponse.EnsureSuccessStatusCode();

        using var payload = await tokenResponse.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.RootElement.GetProperty("access_token").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(payload.RootElement.GetProperty("refresh_token").GetString()));
        Assert.Equal("Bearer", payload.RootElement.GetProperty("token_type").GetString());
    }

    [Fact]
    public async Task RefreshTokenFlow_WhenRefreshTokenValid_ReturnsNewAccessToken()
    {
        await SignInAsAdminAsync();

        var code = await RequestAuthorizationCodeAsync();
        using var codeExchangeResponse = await ExchangeAuthorizationCodeAsync(code);
        codeExchangeResponse.EnsureSuccessStatusCode();

        using var codePayload = await codeExchangeResponse.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(codePayload);
        var refreshToken = codePayload.RootElement.GetProperty("refresh_token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(refreshToken));

        using var refreshResponse = await _client.PostAsync(
            "/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = OpenIddictConstants.GrantTypes.RefreshToken,
                ["client_id"] = WebClientId,
                ["refresh_token"] = refreshToken!,
            }));

        refreshResponse.EnsureSuccessStatusCode();

        using var refreshPayload = await refreshResponse.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(refreshPayload);
        Assert.False(string.IsNullOrWhiteSpace(refreshPayload.RootElement.GetProperty("access_token").GetString()));
        Assert.Equal("Bearer", refreshPayload.RootElement.GetProperty("token_type").GetString());
    }

    [Fact]
    public async Task UserInfo_WithBearerToken_ReturnsExpectedUserClaims()
    {
        await SignInAsAdminAsync();

        var code = await RequestAuthorizationCodeAsync();
        using var tokenResponse = await ExchangeAuthorizationCodeAsync(code);
        tokenResponse.EnsureSuccessStatusCode();

        using var tokenPayload = await tokenResponse.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(tokenPayload);
        var accessToken = tokenPayload.RootElement.GetProperty("access_token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/connect/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(payload);

        var root = payload.RootElement;
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty(OpenIddictConstants.Claims.Subject).GetString()));
        Assert.Equal("admin", GetFirstJsonStringValue(root, OpenIddictConstants.Claims.Name, ClaimTypes.Name));
        Assert.Equal("admin@bzscenter.local", GetFirstJsonStringValue(root, OpenIddictConstants.Claims.Email, ClaimTypes.Email));
        Assert.Contains(IdentitySeedConstants.AdminRoleName, GetJsonStringValues(root, OpenIddictConstants.Claims.Role, ClaimTypes.Role));
        Assert.Contains(PermissionConstants.UsersWrite, GetJsonStringValues(root, PermissionConstants.ClaimType));
    }

    [Fact]
    public async Task AuthorizationCodeFlow_WhenScopesGranted_ProjectsExpectedClaimsIntoTokens()
    {
        await SignInAsAdminAsync();

        var code = await RequestAuthorizationCodeAsync([
            OpenIddictConstants.Scopes.OpenId,
            OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Scopes.Email,
            OpenIddictConstants.Scopes.Roles,
            OpenIddictConstants.Scopes.OfflineAccess,
            PermissionConstants.ScopeApi,
        ]);

        using var tokenResponse = await ExchangeAuthorizationCodeAsync(code);
        tokenResponse.EnsureSuccessStatusCode();

        using var tokenPayload = await ResponseContentToJsonAsync(tokenResponse);
        var accessToken = tokenPayload.RootElement.GetProperty("access_token").GetString();
        var idToken = tokenPayload.RootElement.GetProperty("id_token").GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));
        Assert.False(string.IsNullOrWhiteSpace(idToken));

        using var accessPayload = ReadJwtPayload(accessToken!);
        using var idPayload = ReadJwtPayload(idToken!);

        Assert.Equal("admin", GetFirstJsonStringValue(accessPayload.RootElement, OpenIddictConstants.Claims.Name, ClaimTypes.Name));
        Assert.Equal("admin@bzscenter.local",
            GetFirstJsonStringValue(accessPayload.RootElement, OpenIddictConstants.Claims.Email, ClaimTypes.Email));
        Assert.Contains(IdentitySeedConstants.AdminRoleName,
            GetJsonStringValues(accessPayload.RootElement, OpenIddictConstants.Claims.Role, ClaimTypes.Role));
        Assert.Contains(PermissionConstants.UsersWrite,
            GetJsonStringValues(accessPayload.RootElement, PermissionConstants.ClaimType));

        Assert.Equal("admin", GetFirstJsonStringValue(idPayload.RootElement, OpenIddictConstants.Claims.Name, ClaimTypes.Name));
        Assert.Equal("admin@bzscenter.local",
            GetFirstJsonStringValue(idPayload.RootElement, OpenIddictConstants.Claims.Email, ClaimTypes.Email));
        Assert.Contains(IdentitySeedConstants.AdminRoleName,
            GetJsonStringValues(idPayload.RootElement, OpenIddictConstants.Claims.Role, ClaimTypes.Role));
        Assert.False(idPayload.RootElement.TryGetProperty(PermissionConstants.ClaimType, out _));
    }

    [Fact]
    public async Task AuthorizationCodeFlow_WhenOnlyOpenIdScopeGranted_DoesNotProjectRoleOrPermissionClaims()
    {
        await SignInAsAdminAsync();

        var code = await RequestAuthorizationCodeAsync([
            OpenIddictConstants.Scopes.OpenId,
        ]);

        using var tokenResponse = await ExchangeAuthorizationCodeAsync(code);
        tokenResponse.EnsureSuccessStatusCode();

        using var tokenPayload = await ResponseContentToJsonAsync(tokenResponse);
        var accessToken = tokenPayload.RootElement.GetProperty("access_token").GetString();
        var idToken = tokenPayload.RootElement.GetProperty("id_token").GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));
        Assert.False(string.IsNullOrWhiteSpace(idToken));

        using var accessPayload = ReadJwtPayload(accessToken!);
        using var idPayload = ReadJwtPayload(idToken!);

        Assert.Empty(GetJsonStringValues(accessPayload.RootElement, OpenIddictConstants.Claims.Role, ClaimTypes.Role));
        Assert.False(accessPayload.RootElement.TryGetProperty(PermissionConstants.ClaimType, out _));
        Assert.Null(GetFirstJsonStringValue(idPayload.RootElement, OpenIddictConstants.Claims.Name, ClaimTypes.Name));
        Assert.Null(GetFirstJsonStringValue(idPayload.RootElement, OpenIddictConstants.Claims.Email, ClaimTypes.Email));
        Assert.Empty(GetJsonStringValues(idPayload.RootElement, OpenIddictConstants.Claims.Role, ClaimTypes.Role));
        Assert.False(idPayload.RootElement.TryGetProperty(PermissionConstants.ClaimType, out _));
    }

    [Fact]
    public async Task ClientRegistration_WithAdminCookie_CreatesAndReadsBackClient()
    {
        await SignInAsAdminAsync();

        var request = new OidcClientUpsertRequest
        {
            ClientId = "interactive-client",
            DisplayName = "Interactive Client",
            PublicClient = true,
            GrantTypes = [OpenIddictConstants.GrantTypes.AuthorizationCode, OpenIddictConstants.GrantTypes.RefreshToken],
            Scopes = [OpenIddictConstants.Scopes.OpenId, OpenIddictConstants.Scopes.Profile, PermissionConstants.ScopeApi],
            RedirectUris = ["https://localhost/interactive/callback"],
            PostLogoutRedirectUris = ["https://localhost/interactive/logout-callback"],
        };

        using var createResponse = await _client.PostAsJsonAsync("/api/oidc/clients", request);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<OidcClientRegistrationResponse>();
        Assert.NotNull(created);
        Assert.Equal("interactive-client", created.ClientId);

        using var getResponse = await _client.GetAsync($"/api/oidc/clients/{created.ClientId}");
        getResponse.EnsureSuccessStatusCode();

        var client = await getResponse.Content.ReadFromJsonAsync<OidcClientResponse>();
        Assert.NotNull(client);
        Assert.Equal("interactive-client", client.ClientId);
        Assert.Equal("Interactive Client", client.DisplayName);
        Assert.Contains(OpenIddictConstants.GrantTypes.AuthorizationCode, client.GrantTypes);
        Assert.Contains(PermissionConstants.ScopeApi, client.Scopes);
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
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["IdpIssuer"] = BaseUri.ToString().TrimEnd('/'),
            ["Identity:Admin:UserName"] = "admin",
            ["Identity:Admin:Password"] = "Passw0rd!",
            ["PermissionPolicy:PolicyPrefix"] = PermissionPolicyOptions.DefaultPolicyPrefix,
        });

        builder.Services.AddMemoryCache();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddDbContext<IdpDbContext>(
            options => ConfigureTestDatabase(options, _connection),
            contextLifetime: ServiceLifetime.Scoped,
            optionsLifetime: ServiceLifetime.Singleton);
        builder.Services.AddDbContextFactory<IdpDbContext>(options => ConfigureTestDatabase(options, _connection));

        var registrar = new IdpServiceRegistrar(builder.Services, builder.Configuration);
        registrar.AddIdpOptions();
        registrar.AddDataProtection();
        registrar.AddOidc();

        builder.Services.AddScoped<IRoleService, RoleService>();
        builder.Services.AddScoped<IUserService, UserService>();
        builder.Services.AddScoped<IRolePermissionService, RolePermissionService>();
        builder.Services.AddScoped<IPermissionScopeService, PermissionScopeService>();
        builder.Services.AddScoped<IdentitySeeder>();
        builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

        builder.Services
            .AddControllers()
            .AddApplicationPart(typeof(ConnectController).Assembly);

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapControllers();

        await _app.StartAsync();

        await using (var scope = _app.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IdpDbContext>();
            await dbContext.Database.EnsureCreatedAsync();

            var identitySeeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
            await identitySeeder.SeedAsync();

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<BzsUser>>();
            var admin = await userManager.FindByNameAsync("admin");
            Assert.NotNull(admin);
            admin.Email = "admin@bzscenter.local";
            var updateResult = await userManager.UpdateAsync(admin);
            Assert.True(updateResult.Succeeded, string.Join(", ", updateResult.Errors.Select(static e => e.Description)));

            var applicationManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            await EnsureApplicationAsync(
                applicationManager,
                WebClientId,
                new OidcClientUpsertRequest
                {
                    DisplayName = "Web Client",
                    PublicClient = true,
                    GrantTypes = [OpenIddictConstants.GrantTypes.AuthorizationCode, OpenIddictConstants.GrantTypes.RefreshToken],
                    Scopes = [
                        OpenIddictConstants.Scopes.OpenId,
                        OpenIddictConstants.Scopes.Profile,
                        OpenIddictConstants.Scopes.Email,
                        OpenIddictConstants.Scopes.Roles,
                        OpenIddictConstants.Scopes.OfflineAccess,
                        PermissionConstants.ScopeApi,
                    ],
                    RedirectUris = [WebRedirectUri.ToString()],
                });
            await EnsureApplicationAsync(
                applicationManager,
                MachineClientId,
                new OidcClientUpsertRequest
                {
                    DisplayName = "Machine Client",
                    PublicClient = false,
                    ClientSecret = MachineClientSecret,
                    GrantTypes = [OpenIddictConstants.GrantTypes.ClientCredentials],
                    Scopes = [PermissionConstants.ScopeApi],
                });
        }

        _client = _app.GetTestClient();
        _client.BaseAddress = BaseUri;
        _client.DefaultRequestHeaders.ExpectContinue = false;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static void ConfigureTestDatabase(DbContextOptionsBuilder options, SqliteConnection connection)
    {
        options.UseSqlite(connection);
        options.UseOpenIddict();
    }

    private static async Task EnsureApplicationAsync(
        IOpenIddictApplicationManager applicationManager,
        string clientId,
        OidcClientUpsertRequest request)
    {
        var existingApplication = await applicationManager.FindByClientIdAsync(clientId);
        if (existingApplication is not null)
        {
            return;
        }

        var descriptor = OidcClientDescriptorFactory.CreateDescriptor(request, clientId);
        await applicationManager.CreateAsync(descriptor);
    }

    private async Task SignInAsAdminAsync()
    {
        using var response = await _client.PostAsync(
            "/account/login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["UserName"] = "admin",
                ["Password"] = "Passw0rd!",
                ["RememberMe"] = bool.TrueString,
            }));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);

        var cookieHeaders = response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.Select(static value => value.Split(';', 2)[0]).ToArray()
            : [];

        Assert.NotEmpty(cookieHeaders);
        _authCookieHeader = string.Join("; ", cookieHeaders);
        _client.DefaultRequestHeaders.Remove("Cookie");
        _client.DefaultRequestHeaders.Add("Cookie", _authCookieHeader);
    }

    private async Task<string> RequestAuthorizationCodeAsync(IEnumerable<string>? scopes = null)
    {
        var requestedScopes = scopes?.ToArray() ??
            [
                OpenIddictConstants.Scopes.OpenId,
                OpenIddictConstants.Scopes.Profile,
                OpenIddictConstants.Scopes.Email,
                OpenIddictConstants.Scopes.Roles,
                OpenIddictConstants.Scopes.OfflineAccess,
                PermissionConstants.ScopeApi,
            ];

        var authorizeUrl = QueryHelpers.AddQueryString(
            "/connect/authorize",
            new Dictionary<string, string?>
            {
                ["client_id"] = WebClientId,
                ["response_type"] = OpenIddictConstants.ResponseTypes.Code,
                ["redirect_uri"] = WebRedirectUri.ToString(),
                ["scope"] = string.Join(' ', requestedScopes),
                ["state"] = "test-state",
                ["code_challenge"] = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
                ["code_challenge_method"] = "S256",
            });

        using var request = new HttpRequestMessage(HttpMethod.Get, authorizeUrl);
        request.Headers.Accept.ParseAdd("text/html");

        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        if (response.StatusCode != HttpStatusCode.Found)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException(
                $"Expected 302 redirect from /connect/authorize but got {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
        }

        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.Equal(WebRedirectUri.GetLeftPart(UriPartial.Path), location.GetLeftPart(UriPartial.Path));

        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.True(query.TryGetValue("code", out var codeValues));
        var code = codeValues.ToString();
        Assert.False(string.IsNullOrWhiteSpace(code));

        return code;
    }

    private Task<HttpResponseMessage> ExchangeAuthorizationCodeAsync(string code)
    {
        return _client.PostAsync(
            "/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = OpenIddictConstants.GrantTypes.AuthorizationCode,
                ["client_id"] = WebClientId,
                ["code"] = code,
                ["redirect_uri"] = WebRedirectUri.ToString(),
                ["code_verifier"] = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk",
            }));
    }

    private static async Task<JsonDocument> ResponseContentToJsonAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(payload);
        return payload;
    }

    private static JsonDocument ReadJwtPayload(string token)
    {
        var segments = token.Split('.');
        Assert.True(segments.Length >= 2, "JWT token must contain at least header and payload segments.");

        var payloadBytes = WebEncoders.Base64UrlDecode(segments[1]);
        return JsonDocument.Parse(payloadBytes);
    }

    private static string? GetFirstJsonStringValue(JsonElement root, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!root.TryGetProperty(propertyName, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.String)
            {
                return property.GetString();
            }

            var first = property.ValueKind == JsonValueKind.Array
                ? property.EnumerateArray().Select(static item => item.GetString()).OfType<string>().FirstOrDefault()
                : null;

            if (!string.IsNullOrWhiteSpace(first))
            {
                return first;
            }
        }

        return null;
    }

    private static string[] GetJsonStringValues(JsonElement root, params string[] propertyNames)
    {
        var values = new List<string>();

        foreach (var propertyName in propertyNames)
        {
            if (!root.TryGetProperty(propertyName, out var property))
            {
                continue;
            }

            switch (property.ValueKind)
            {
                case JsonValueKind.Array:
                    values.AddRange(property.EnumerateArray()
                        .Select(static item => item.GetString())
                        .OfType<string>());
                    break;
                case JsonValueKind.String:
                    if (!string.IsNullOrWhiteSpace(property.GetString()))
                    {
                        values.Add(property.GetString()!);
                    }

                    break;
            }
        }

        return values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
