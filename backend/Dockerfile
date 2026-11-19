# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy solution and project definitions
COPY BahaGuard.sln ./
COPY src/BahaGuard.Domain/BahaGuard.Domain.csproj src/BahaGuard.Domain/
COPY src/BahaGuard.Application/BahaGuard.Application.csproj src/BahaGuard.Application/
COPY src/BahaGuard.Infrastructure/BahaGuard.Infrastructure.csproj src/BahaGuard.Infrastructure/
COPY src/BahaGuard.Api/BahaGuard.Api.csproj src/BahaGuard.Api/

# Restore dependencies
RUN dotnet restore BahaGuard.sln

# Copy the rest of the source code
COPY src/ src/

# Publish Api project
WORKDIR /app/src/BahaGuard.Api
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

# Directory for persisted image uploads
RUN mkdir -p /app/uploads/reports

ENTRYPOINT ["dotnet", "BahaGuard.Api.dll"]
