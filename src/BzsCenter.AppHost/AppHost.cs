using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var environmentName = builder.Environment.EnvironmentName;
var adminUserName = builder.Configuration["Identity:Admin:UserName"];
var adminPassword = builder.Configuration["Identity:Admin:Password"];

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
