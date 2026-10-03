# UserService.API – context build là THƯ MỤC GỐC repo (xem .github/workflows/cd.yml → context: .)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy theo lớp để tận dụng cache: shared trước, service sau
COPY Directory.Build.props ./
COPY BuildingBlocks ./BuildingBlocks
COPY Services/UserService ./Services/UserService

RUN dotnet publish Services/UserService/UserService.API/UserService.API.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "UserService.API.dll"]