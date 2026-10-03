# NotificationService.API – context build là THƯ MỤC GỐC repo (xem .github/workflows/cd.yml → context: .)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY BuildingBlocks ./BuildingBlocks
COPY Services/NotificationService ./Services/NotificationService

RUN dotnet publish Services/NotificationService/NotificationService.API/NotificationService.API.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "NotificationService.API.dll"]