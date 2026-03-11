FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
ENV MSBuildEnableWorkloadResolver=false
COPY SecurityRestApi/SecurityRestApi.csproj .
RUN dotnet restore
COPY SecurityRestApi/ .
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SecurityRestApi.dll"]
