# Etapa base (solo runtime)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# Etapa de compilación
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["WarframeInventory/WarframeInventory/WarframeInventory.csproj", "WarframeInventory/WarframeInventory/"]
RUN dotnet restore "WarframeInventory/WarframeInventory/WarframeInventory.csproj"
COPY ["WarframeInventory/WarframeInventory/", "WarframeInventory/WarframeInventory/"]
RUN dotnet publish "WarframeInventory/WarframeInventory/WarframeInventory.csproj" -c Release --no-restore -o /app/out

# Imagen final
FROM base AS final
WORKDIR /app
COPY --from=build /app/out .
# Production configuration is immutable inside the image. Disabling JSON
# reload prevents ASP.NET Core from allocating inotify watchers, which can
# exceed the shared kernel limit on small Render instances before startup.
ENTRYPOINT ["dotnet", "WarframeInventory.dll", "--hostBuilder:reloadConfigOnChange=false"]
