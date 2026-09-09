# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:9.0-bookworm-slim AS build
WORKDIR /src

COPY . .
RUN dotnet restore src/AgeNexus.Web/AgeNexus.Web.csproj
RUN dotnet publish src/AgeNexus.Web/AgeNexus.Web.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS final

WORKDIR /app
COPY --from=build /app/publish .

USER $APP_UID

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_EnableDiagnostics=0

EXPOSE 10000

CMD ["sh", "-c", "exec dotnet AgeNexus.Web.dll --urls http://0.0.0.0:${PORT:-10000}"]
