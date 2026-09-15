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

ARG AGEXTRACTOR_COMMIT=214a03e34a76efd1dfbca3fb50fb954a2b8307d1

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl python3 python3-venv tesseract-ocr \
    && rm -rf /var/lib/apt/lists/* \
    && python3 -m venv /opt/agextractor-venv \
    && mkdir -p /opt/agextractor \
    && curl --fail --location --silent --show-error \
       "https://github.com/apollw/AgeXtractor/archive/${AGEXTRACTOR_COMMIT}.tar.gz" \
       | tar --extract --gzip --strip-components=1 --directory=/opt/agextractor \
    && /opt/agextractor-venv/bin/pip install --no-cache-dir --disable-pip-version-check \
       -r /opt/agextractor/requirements-server.txt

WORKDIR /app
COPY --from=build /app/publish .

USER $APP_UID

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_EnableDiagnostics=0
ENV AgeExtractor__PythonExecutable=/opt/agextractor-venv/bin/python
ENV AgeExtractor__WorkingDirectory=/opt/agextractor
ENV PYTHONDONTWRITEBYTECODE=1

EXPOSE 10000

CMD ["sh", "-c", "exec dotnet AgeNexus.Web.dll --urls http://0.0.0.0:${PORT:-10000}"]
