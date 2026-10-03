# API Gateway (YARP) – context build là THƯ MỤC GỐC repo (xem .github/workflows/cd.yml → context: .)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY Gateway ./Gateway

RUN dotnet publish Gateway/ParkingManagement.Gateway/ParkingManagement.Gateway.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ParkingManagement.Gateway.dll"]