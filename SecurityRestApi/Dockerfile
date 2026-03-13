# =============================================================================
# Wzorcowy multi-stage Dockerfile dla ASP.NET Core Web API (.NET 8/9)
# =============================================================================
#
# NAJCZĘSTSZY POWÓD BŁĘDU "does not contain a project or solution file":
#
#   RUN dotnet restore           ← MSBuild szuka .csproj w WORKDIR
#   RUN dotnet restore .         ← j.w.
#   RUN dotnet restore *.csproj  ← OK tylko gdy .csproj jest bezpośrednio w WORKDIR
#
# ROZWIĄZANIE: zawsze podawaj jawną ścieżkę do pliku projektu.
#
# =============================================================================
# Dostosuj zmienne:
ARG PROJECT_NAME=SecurityRestApi
ARG DOTNET_VERSION=10.0

# ── Stage 1: Restore + Build ──────────────────────────────────────────────
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

# Kopiuj TYLKO plik projektu najpierw → Docker/Kaniko cachuje warstwę restore
# Dopóki .csproj się nie zmieni, `dotnet restore` nie będzie ponownie pobierał pakietów
COPY ${PROJECT_NAME}/${PROJECT_NAME}.csproj ./${PROJECT_NAME}/

# Restore z jawną ścieżką — nigdy nie będzie problemu z "brak .csproj w CWD"
RUN dotnet restore ./${PROJECT_NAME}/${PROJECT_NAME}.csproj

# Kopiuj resztę kodu (po restore → cache restore layer jest zachowany)
COPY ${PROJECT_NAME}/ ./${PROJECT_NAME}/

# Build w trybie Release
RUN dotnet build ./${PROJECT_NAME}/${PROJECT_NAME}.csproj \
    --configuration Release \
    --no-restore \
    --output /app/build

# ── Stage 2: Publish ──────────────────────────────────────────────────────
FROM build AS publish
RUN dotnet publish ./${PROJECT_NAME}/${PROJECT_NAME}.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

# ── Stage 3: Runtime (finalny obraz — jak najmniejszy) ───────────────────
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final

# Nie uruchamiaj jako root
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
WORKDIR /app

COPY --from=publish /app/publish .

# Zmień właściciela plików na non-root user
RUN chown -R appuser:appgroup /app
USER appuser

# Port ASP.NET Core (Kestrel)
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "SecurityRestApi.dll"]
