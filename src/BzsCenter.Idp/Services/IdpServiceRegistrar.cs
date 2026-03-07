using BzsCenter.Idp.Domain;
using BzsCenter.Idp.Infra;
using BzsCenter.Idp.Infra.Oidc;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;

namespace BzsCenter.Idp.Services;

/// <summary>
/// IDP 服务注册协调器：统一装配选项、DataProtection 与 OIDC。
/// </summary>
internal sealed class IdpServiceRegistrar(IServiceCollection sc, IConfiguration cfg)
{
    private const string ForwardedHeadersSectionName = "ForwardedHeaders";
    private const string DataProtectionSectionName = "DataProtection";
    private const string OidcSectionName = "Oidc";

    internal IServiceCollection AddIdpOptions()
    {
        sc.AddOptions<BzsForwardedHeadersOptions>().Bind(cfg.GetSection(ForwardedHeadersSectionName));
        sc.AddOptions<DataProtectionOptions>().Bind(cfg.GetSection(DataProtectionSectionName));
        sc.AddOptions<OidcOptions>().Bind(cfg.GetSection(OidcSectionName));
        return sc;
    }

    internal IServiceCollection AddDataProtection()
    {
#if !DEBUG
        var options = cfg.GetSection(DataProtectionSectionName).Get<DataProtectionOptions>();
        if (options is null)
        {
            throw new InvalidOperationException("DataProtection options are not configured.");
        }
#else
        var options = new DataProtectionOptions()
        {
            ApplicationName = "BzsCenter.Idp.Test",
            KeyLifetimeDays = 365,
            StorageDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DevDataProtectionKeys"),
        };
#endif

        sc.AddDataProtectionKeyStorage(options);
        return sc;
    }

    internal IServiceCollection AddOidc()
    {
        var oidcOptions = cfg.GetSection(OidcSectionName).Get<OidcOptions>();

        sc.AddAuthorization();
        ConfigureAuthenticationSchemes();
        ConfigureIdentity();
        ConfigureApplicationCookie();
        ConfigureOpenIddict(oidcOptions);

        return sc;
    }

    private void ConfigureAuthenticationSchemes()
    {
        sc.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
            options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
        });
    }

    private void ConfigureIdentity()
    {
        sc.AddIdentity<BzsUser, BzsRole>(opt =>
            {
                opt.SignIn.RequireConfirmedEmail = false;
                opt.Password.RequireUppercase = false;
                opt.Password.RequireLowercase = false;
                opt.Password.RequiredLength = 6;
                opt.Password.RequireNonAlphanumeric = false;
                opt.Password.RequiredUniqueChars = 0;
                opt.Password.RequireDigit = true;
            })
            .AddEntityFrameworkStores<IdpDbContext>()
            .AddDefaultTokenProviders()
            .AddErrorDescriber<ChineseIdentityErrorDescriber>();
    }

    private void ConfigureApplicationCookie()
    {
        sc.ConfigureApplicationCookie(opt =>
        {
            opt.LoginPath = "/account/login";
            opt.LogoutPath = "/account/logout";
            opt.AccessDeniedPath = "/account/denied";
            opt.Cookie.Name = "bzs.auth";
            opt.Cookie.Path = "/";
            opt.Cookie.SameSite = SameSiteMode.Lax;
            opt.Cookie.HttpOnly = true;
            opt.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            opt.ExpireTimeSpan = TimeSpan.FromHours(36);
            opt.SlidingExpiration = true;
        });
    }

    private void ConfigureOpenIddict(OidcOptions? oidcOptions)
    {
        sc.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore().UseDbContext<IdpDbContext>();
            })
            .AddServer(options =>
            {
                ConfigureOpenIddictEndpointsAndFlows(options);
                ConfigureOpenIddictCertificates(options, oidcOptions);
                ConfigureOpenIddictAspNetCore(options);
                ConfigureOpenIddictTokenLifetimes(options, oidcOptions);
            });
    }

    private void ConfigureOpenIddictEndpointsAndFlows(OpenIddictServerBuilder options)
    {
        var idpIssuer = cfg.GetRequiredSection("IdpIssuer").Value!;

        options
            .SetIssuer(new Uri(idpIssuer))
            .SetTokenEndpointUris("connect/token")
            .SetAuthorizationEndpointUris("connect/authorize")
            .SetEndSessionEndpointUris("connect/logout")
            .SetUserInfoEndpointUris("connect/userinfo")
            .AllowAuthorizationCodeFlow()
            .AllowRefreshTokenFlow()
            .RequireProofKeyForCodeExchange()
            .AllowClientCredentialsFlow();

        options.RegisterScopes(
            OpenIddictConstants.Scopes.OpenId,
            OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Scopes.Email,
            OpenIddictConstants.Scopes.Roles,
            "api.read",
            "api.write",
            OpenIddictConstants.Scopes.OfflineAccess);
    }

    private void ConfigureOpenIddictCertificates(OpenIddictServerBuilder options, OidcOptions? oidcOptions)
    {
#if DEBUG
        // DEBUG 只使用开发证书，避免本地环境引入证书文件依赖。
        options.AddDevelopmentEncryptionCertificate()
            .AddDevelopmentSigningCertificate();
#else
        if (oidcOptions is null)
        {
            throw new InvalidOperationException("Oidc options are not configured.");
        }

        var (signingPaths, signingPasswords, signingOptionName) =
            OidcCertificateLoader.ResolveSigningCertificateConfig(oidcOptions);
        var encryptionPaths = oidcOptions.EncryptionCertificatePath;
        var encryptionPasswords = oidcOptions.EncryptionCertificatePassword;

        foreach (var cert in OidcCertificateLoader.LoadCertificates(signingPaths, signingPasswords, signingOptionName))
        {
            options.AddSigningCertificate(cert);
        }

        foreach (var cert in OidcCertificateLoader.LoadCertificates(
                     encryptionPaths,
                     encryptionPasswords,
                     "Oidc:EncryptionCertificatePath"))
        {
            options.AddEncryptionCertificate(cert);
        }
#endif
    }

    private static void ConfigureOpenIddictAspNetCore(OpenIddictServerBuilder options)
    {
        options.UseAspNetCore()
            .EnableTokenEndpointPassthrough()
            .EnableAuthorizationEndpointPassthrough()
            .EnableEndSessionEndpointPassthrough()
            .EnableUserInfoEndpointPassthrough();
    }

    private static void ConfigureOpenIddictTokenLifetimes(
        OpenIddictServerBuilder options,
        OidcOptions? oidcOptions)
    {
        var accessTokenLifetimeMinutes = oidcOptions?.AccessTokenLifetimeMinutes ?? 45;
        var refreshTokenLifetimeDays = oidcOptions?.RefreshTokenLifetimeDays ?? 7;

        if (accessTokenLifetimeMinutes <= 0)
        {
            throw new InvalidOperationException("Oidc:AccessTokenLifetimeMinutes must be greater than 0.");
        }

        if (refreshTokenLifetimeDays <= 0)
        {
            throw new InvalidOperationException("Oidc:RefreshTokenLifetimeDays must be greater than 0.");
        }

        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(accessTokenLifetimeMinutes));
        options.SetRefreshTokenLifetime(TimeSpan.FromDays(refreshTokenLifetimeDays));

        if (oidcOptions?.DisableAccessTokenEncryption ?? true)
        {
            options.DisableAccessTokenEncryption();
        }
    }
}
