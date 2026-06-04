# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Api.csproj", "src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Api/"]
COPY ["src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Infrastructure/Cycling.Rider.Tracking.Infrastructure.csproj", "src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Infrastructure/"]
COPY ["src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Application/Cycling.Rider.Tracking.Application.csproj", "src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Application/"]
COPY ["src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Domain/Cycling.Rider.Tracking.Domain.csproj", "src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Domain/"]
RUN dotnet restore "./src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Api.csproj"
COPY . .
WORKDIR "/src/src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Api"
RUN dotnet build "./Cycling.Rider.Tracking.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Cycling.Rider.Tracking.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Cycling.Rider.Tracking.Api.dll"]