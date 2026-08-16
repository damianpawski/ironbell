# syntax=docker/dockerfile:1

# Multi-stage on purpose: the image builds from source inside Docker, so it does not depend on
# whatever happens to be installed on the machine that runs the build. CI and a laptop produce the
# same thing.

# The tag must satisfy global.json, which pins SDK 10.0.400 with rollForward: latestFeature. A
# lower feature band in this image fails the restore rather than silently building against
# something else, which is the failure mode worth having.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Manifests first, so editing code does not invalidate the restore layer.
COPY global.json Directory.Build.props Directory.Packages.props ./
# build/ holds targets imported by Ironbell.Api.csproj. Imports resolve when the project loads, so
# this has to be here before restore, not alongside the source.
COPY build/ build/
COPY src/Ironbell.Domain/Ironbell.Domain.csproj src/Ironbell.Domain/
COPY src/Ironbell.Infrastructure/Ironbell.Infrastructure.csproj src/Ironbell.Infrastructure/
COPY src/Ironbell.Client/Ironbell.Client.csproj src/Ironbell.Client/
COPY src/Ironbell.Api/Ironbell.Api.csproj src/Ironbell.Api/
RUN dotnet restore src/Ironbell.Api/Ironbell.Api.csproj

COPY src/ src/

# Publishing the API also publishes the client into its wwwroot: the Api project references the
# Client project, so one container serves both on one origin. That is what keeps the refresh cookie
# first-party and leaves no CORS story to design.
RUN dotnet publish src/Ironbell.Api/Ironbell.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# WORKDIR before ENTRYPOINT is load-bearing, not habit. WebApplication.CreateBuilder takes the
# content root from the current directory, so starting from anywhere else serves the API fine and
# 404s every static file — the client silently disappears while health checks stay green.
WORKDIR /app

COPY --from=build /app/publish .

# Non-root. APP_UID is defined by the base image.
USER $APP_UID

# Container Apps routes to this port; the app never assumes a hostname.
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

# No migrations here. They are applied as a pipeline step before this image is rolled out, so a
# container that starts cannot alter the schema and several replicas cannot race each other.
ENTRYPOINT ["dotnet", "Ironbell.Api.dll"]
