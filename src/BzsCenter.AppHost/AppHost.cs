using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var environmentName = builder.Environment.EnvironmentName;
var adminUserName = builder.Configuration["Identity:Admin:UserName"];
var adminPassword = builder.Configuration["Identity:Admin:Password"];
var e2eTestingEnabled = builder.Configuration["Testing:E2E:Enabled"];
var gitHubClientId = builder.Configuration["Authentication:GitHub:ClientId"];
var gitHubClientSecret = builder.Configuration["Authentication:GitHub:ClientSecret"];
var gitHubCallbackPath = builder.Configuration["Authentication:GitHub:CallbackPath"];

if (string.IsNullOrWhiteSpace(adminUserName) && builder.Environment.IsDevelopment())
{
    adminUserName = "admin";
}

if (string.IsNullOrWhiteSpace(adminPassword) && builder.Environment.IsDevelopment())
{
    adminPassword = "Passw0rd!";
}

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();

var idpDatabase = postgres.AddDatabase("DefaultConnection");
var redis = builder.AddRedis("redis");

var idp = builder.AddProject<Projects.BzsCenter_Idp>("idp")
    .WithEnvironment("DOTNET_ENVIRONMENT", environmentName)
    .WithEnvironment("Identity__Admin__UserName", adminUserName)
    .WithEnvironment("Identity__Admin__Password", adminPassword)
    .WithEnvironment("CacheOptions__CacheType", "Redis")
    .WithReference(idpDatabase)
    .WithReference(redis);

if (!string.IsNullOrWhiteSpace(e2eTestingEnabled))
{
    idp = idp.WithEnvironment("Testing__E2E__Enabled", e2eTestingEnabled);
}

if (!string.IsNullOrWhiteSpace(gitHubClientId))
{
    idp = idp.WithEnvironment("Authentication__GitHub__ClientId", gitHubClientId);
}

if (!string.IsNullOrWhiteSpace(gitHubClientSecret))
{
    idp = idp.WithEnvironment("Authentication__GitHub__ClientSecret", gitHubClientSecret);
}

if (!string.IsNullOrWhiteSpace(gitHubCallbackPath))
{
    idp = idp.WithEnvironment("Authentication__GitHub__CallbackPath", gitHubCallbackPath);
}

var idpMigrator = builder.AddProject<Projects.BzsCenter_Idp_Migrator>("idp-migrator")
    .WithEnvironment("DOTNET_ENVIRONMENT", environmentName)
    .WithEnvironment("IdpIssuer", idp.GetEndpoint("https"))
    .WithEnvironment("Identity__Admin__UserName", adminUserName)
    .WithEnvironment("Identity__Admin__Password", adminPassword)
    .WithReference(idpDatabase)
    .WaitFor(postgres);

idp
    .WithEnvironment("IdpIssuer", idp.GetEndpoint("https"))
    .WaitFor(postgres)
    .WaitFor(redis)
    .WaitForCompletion(idpMigrator);

builder.Build().Run();
