# Etapa base (solo runtime)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 10000

# Etapa de compilación
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["WarframeInventory/WarframeInventory/WarframeInventory.csproj", "WarframeInventory/WarframeInventory/"]
RUN dotnet restore "WarframeInventory/WarframeInventory/WarframeInventory.csproj"
COPY . .
RUN dotnet publish "WarframeInventory/WarframeInventory/WarframeInventory.csproj" -c Release --no-restore -o /app/out

# Imagen final
FROM base AS final
WORKDIR /app
COPY --from=build /app/out .
ENV ASPNETCORE_URLS=http://0.0.0.0:10000
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
ENTRYPOINT ["dotnet", "WarframeInventory.dll"]
