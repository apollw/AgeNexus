# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:9.0-bookworm-slim AS build
WORKDIR /src

COPY . .
RUN dotnet restore AgeNexus.slnx
RUN dotnet publish src/AgeNexus.Web/AgeNexus.Web.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS final

RUN apt-get update \
    && apt-get install --yes --no-install-recommends python3 python3-venv \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /src/src/AgeNexus.Infrastructure/ReplayAnalysis ./ReplayAnalysis

RUN python3 -m venv /opt/agenexus-python \
    && /opt/agenexus-python/bin/pip install --no-cache-dir \
        --requirement ReplayAnalysis/requirements.txt

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_EnableDiagnostics=0
ENV PATH="/opt/agenexus-python/bin:${PATH}"

EXPOSE 10000

CMD ["sh", "-c", "exec dotnet AgeNexus.Web.dll --urls http://0.0.0.0:${PORT:-10000}"]
