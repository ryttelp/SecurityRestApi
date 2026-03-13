# =============================================================================
# Dockerfile dla SecurityRestApi — ASP.NET Core Web API (.NET 10)
#
# Lokalizacja w repo:  SecurityRestApi/Dockerfile
# Kaniko context:      SecurityRestApi/          (context-sub-path=SecurityRestApi/)
# Kaniko dockerfile:   SecurityRestApi/Dockerfile
#
# Struktura plików widziana przez Kaniko (root contextu = SecurityRestApi/):
#   ./SecurityRestApi.csproj   ← tu, nie SecurityRestApi/SecurityRestApi.csproj
#   ./Program.cs
#   ./Controllers/
#   ./Dockerfile
#   (itd.)
# =============================================================================

ARG DOTNET_VERSION=10.0

# ── Stage 1: Restore ─────────────────────────────────────────────────────────
# Uwaga: NIE używaj --platform w FROM przy Kaniko — platforma jest sterowana
# flagą --customPlatform na poziomie Kaniko executora, nie w Dockerfile.
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS restore
ARG DOTNET_VERSION

WORKDIR /src

# COPY z jawną nazwą pliku — żadnych zmiennych ARG w ścieżkach COPY.
# Context = SecurityRestApi/, więc .csproj leży bezpośrednio w rocie contextu.
COPY SecurityRestApi.csproj ./

RUN dotnet restore SecurityRestApi.csproj

# ── Stage 2: Build ───────────────────────────────────────────────────────────
FROM restore AS build

# Kopiuj resztę źródeł (warstwa restore jest już zcache'owana)
COPY . .

RUN dotnet build SecurityRestApi.csproj \
    --configuration Release \
    --no-restore \
    --output /app/build

# ── Stage 3: Publish ─────────────────────────────────────────────────────────
FROM build AS publish

RUN dotnet publish SecurityRestApi.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

# ── Stage 4: Runtime (finalny obraz) ─────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final

RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
WORKDIR /app

COPY --from=publish /app/publish .
RUN chown -R appuser:appgroup /app
USER appuser

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "SecurityRestApi.dll"]