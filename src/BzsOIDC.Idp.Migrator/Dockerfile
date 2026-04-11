FROM node:22-bookworm-slim AS node

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

COPY --from=node /usr/local/ /usr/local/

WORKDIR /src

COPY ["BzsOIDC.sln", "."]
COPY ["src/BzsOIDC.AppHost.ServiceDefaults/BzsOIDC.AppHost.ServiceDefaults.csproj", "src/BzsOIDC.AppHost.ServiceDefaults/"]
COPY ["src/BzsOIDC.Idp.Client/BzsOIDC.Idp.Client.csproj", "src/BzsOIDC.Idp.Client/"]
COPY ["src/BzsOIDC.Idp/BzsOIDC.Idp.csproj", "src/BzsOIDC.Idp/"]
COPY ["src/BzsOIDC.Idp.Migrator/BzsOIDC.Idp.Migrator.csproj", "src/BzsOIDC.Idp.Migrator/"]
COPY ["src/Shared/BzsOIDC.Shared.Infrastructure/BzsOIDC.Shared.Infrastructure.csproj", "src/Shared/BzsOIDC.Shared.Infrastructure/"]
COPY ["src/BzsOIDC.Idp/package.json", "src/BzsOIDC.Idp/"]
COPY ["src/BzsOIDC.Idp/package-lock.json", "src/BzsOIDC.Idp/"]

RUN dotnet restore "src/BzsOIDC.Idp.Migrator/BzsOIDC.Idp.Migrator.csproj"

COPY . .

WORKDIR /src/src/BzsOIDC.Idp
RUN npm ci

WORKDIR /src
RUN dotnet publish "src/BzsOIDC.Idp.Migrator/BzsOIDC.Idp.Migrator.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "BzsOIDC.Idp.Migrator.dll"]
