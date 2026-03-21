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

# ── Stage 1: Build ───────────────────────────────────────────────────────────
# Restore i build w JEDNYM stage — Kaniko nie przenosi NuGet cache między
# oddzielnymi stage'ami (problem z cross-stage /root/.nuget/packages/).
# Osobna warstwa COPY *.csproj + RUN dotnet restore nadal daje cache:
#   → zmiana kodu źródłowego nie unieważnia warstwy z paczkami NuGet
#   → tylko zmiana .csproj wymusza ponowny restore
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
ARG DOTNET_VERSION

WORKDIR /src

# Kaniko nie przechwytuje /root/.nuget/packages między warstwami RUN.
# NUGET_PACKAGES wskazuje na ścieżkę wewnątrz WORKDIR — Kaniko ją snapshottuje poprawnie.
ENV NUGET_PACKAGES=/src/.nuget/packages

COPY SecurityRestApi.csproj ./
RUN dotnet restore SecurityRestApi.csproj

COPY . .

RUN dotnet build SecurityRestApi.csproj \
    --configuration Release \
    --no-restore \
    --output /app/build

# ── Stage 2: Publish ─────────────────────────────────────────────────────────
FROM build AS publish

RUN dotnet publish SecurityRestApi.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

# ── Stage 4: Runtime (finalny obraz) ─────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final

# Od .NET 8 obraz bazowy zawiera już wbudowanego użytkownika "app" (UID 1654).
# NIE używaj addgroup/adduser — to komendy Alpine/Busybox, niedostępne na Ubuntu Chiseled.
WORKDIR /app

COPY --from=publish /app/publish .

USER app

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "SecurityRestApi.dll"]