FROM node:22-bookworm-slim AS node

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

COPY --from=node /usr/local/ /usr/local/

WORKDIR /src

COPY ["BzsCenter.sln", "."]
COPY ["src/BzsCenter.AppHost.ServiceDefaults/BzsCenter.AppHost.ServiceDefaults.csproj", "src/BzsCenter.AppHost.ServiceDefaults/"]
COPY ["src/BzsCenter.Idp.Client/BzsCenter.Idp.Client.csproj", "src/BzsCenter.Idp.Client/"]
COPY ["src/BzsCenter.Idp/BzsCenter.Idp.csproj", "src/BzsCenter.Idp/"]
COPY ["src/BzsCenter.Idp.Migrator/BzsCenter.Idp.Migrator.csproj", "src/BzsCenter.Idp.Migrator/"]
COPY ["src/Shared/BzsCenter.Shared.Infrastructure/BzsCenter.Shared.Infrastructure.csproj", "src/Shared/BzsCenter.Shared.Infrastructure/"]
COPY ["src/BzsCenter.Idp/package.json", "src/BzsCenter.Idp/"]
COPY ["src/BzsCenter.Idp/package-lock.json", "src/BzsCenter.Idp/"]

RUN dotnet restore "src/BzsCenter.Idp.Migrator/BzsCenter.Idp.Migrator.csproj"

COPY . .

WORKDIR /src/src/BzsCenter.Idp
RUN npm ci

WORKDIR /src
RUN dotnet publish "src/BzsCenter.Idp.Migrator/BzsCenter.Idp.Migrator.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "BzsCenter.Idp.Migrator.dll"]
