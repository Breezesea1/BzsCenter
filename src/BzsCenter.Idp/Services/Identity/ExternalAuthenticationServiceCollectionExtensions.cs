using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace BzsCenter.Idp.Services.Identity;

internal static class ExternalAuthenticationServiceCollectionExtensions
{
    private const string AuthenticationSectionName = "Authentication";

    internal static IServiceCollection AddExternalAuthenticationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ExternalAuthenticationOptions>()
            .Bind(configuration.GetSection(AuthenticationSectionName))
            .Validate(static options =>
                options.GitHub.IsConfigured ||
                (string.IsNullOrWhiteSpace(options.GitHub.ClientId) && string.IsNullOrWhiteSpace(options.GitHub.ClientSecret)),
                "Authentication:GitHub must specify both ClientId and ClientSecret.")
            .ValidateOnStart();

        services.AddSingleton<IExternalLoginProviderStore, ExternalLoginProviderStore>();
        services.AddScoped<IExternalLoginService, ExternalLoginService>();

        return services;
    }

    internal static AuthenticationBuilder AddConfiguredExternalAuthenticationProviders(
        this AuthenticationBuilder authenticationBuilder,
        IConfiguration configuration)
    {
        var options = configuration.GetSection(AuthenticationSectionName).Get<ExternalAuthenticationOptions>() ?? new ExternalAuthenticationOptions();

        if (options.GitHub.IsConfigured)
        {
            authenticationBuilder.AddGitHub(gitHubOptions =>
            {
                gitHubOptions.SignInScheme = IdentityConstants.ExternalScheme;
                gitHubOptions.ClientId = options.GitHub.ClientId;
                gitHubOptions.ClientSecret = options.GitHub.ClientSecret;
                gitHubOptions.CallbackPath = options.GitHub.CallbackPath;
                gitHubOptions.Scope.Add("user:email");
                gitHubOptions.ClaimActions.MapJsonKey("urn:github:name", "name");
                gitHubOptions.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
            });
        }

        return authenticationBuilder;
    }
}
