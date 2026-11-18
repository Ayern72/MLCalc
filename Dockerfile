# The MLCalc web app: the ASP.NET Core server with the Blazor WebAssembly client it serves.
#   docker build -t mlcalc-web .
#   docker run --rm -p 8080:8080 mlcalc-web        then open http://localhost:8080
# compose.yaml does the same with the limits spelled out.

# ───────────── Build ─────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# The project files first, so that the restore layer is reused while only code changes.
COPY global.json Directory.Build.props Directory.Build.targets Directory.Packages.props ./
COPY src/MLCalc.Mathematics/MLCalc.Mathematics.csproj src/MLCalc.Mathematics/
COPY src/MLCalc.LinearAlgebra/MLCalc.LinearAlgebra.csproj src/MLCalc.LinearAlgebra/
COPY src/MLCalc.Numerics/MLCalc.Numerics.csproj src/MLCalc.Numerics/
COPY src/MLCalc.Language/MLCalc.Language.csproj src/MLCalc.Language/
COPY src/MLCalc.Interpreter/MLCalc.Interpreter.csproj src/MLCalc.Interpreter/
COPY src/MLCalc.Plotting/MLCalc.Plotting.csproj src/MLCalc.Plotting/
COPY src/MLCalc.Web.Protocol/MLCalc.Web.Protocol.csproj src/MLCalc.Web.Protocol/
COPY src/MLCalc.Web.Client/MLCalc.Web.Client.csproj src/MLCalc.Web.Client/
COPY src/MLCalc.Web.Server/MLCalc.Web.Server.csproj src/MLCalc.Web.Server/
RUN dotnet restore src/MLCalc.Web.Server/MLCalc.Web.Server.csproj

COPY src/ src/
COPY packaging/ packaging/
RUN dotnet publish src/MLCalc.Web.Server/MLCalc.Web.Server.csproj -c Release --no-restore -o /app

# ───────────── Run ─────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# The image's own unprivileged user; the server listens on port 8080 (ASPNETCORE_HTTP_PORTS).
USER app
EXPOSE 8080
ENV ASPNETCORE_ENVIRONMENT=Production

# The image has no curl or wget for a HEALTHCHECK; GET /health is meant for the platform's own
# health probe, such as the probes in deploy/cloudrun.yaml.
ENTRYPOINT ["dotnet", "MLCalc.Web.Server.dll"]
