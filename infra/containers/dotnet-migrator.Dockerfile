# Builds a self-contained EF Core migrations bundle and applies it once, so schema changes go
# through a single controlled step (the apps never migrate on startup — see AGENTS.md/README).
FROM mcr.microsoft.com/dotnet/sdk:10.0.301@sha256:ea8bde36c11b6e7eec2656d0e59101d4462f6bd630730f2c8201ed0572b295d5 AS build
WORKDIR /src

COPY . .
RUN dotnet restore src/dotnet/PersonalTechBrief.sln --locked-mode
RUN dotnet tool restore
RUN dotnet tool run dotnet-ef migrations bundle \
    --project src/dotnet/PersonalTechBrief.Infrastructure/PersonalTechBrief.Infrastructure.csproj \
    --startup-project src/dotnet/PersonalTechBrief.Web/PersonalTechBrief.Web.csproj \
    --configuration Release \
    --output /app/efbundle

FROM mcr.microsoft.com/dotnet/aspnet:10.0.9@sha256:7644f992230d35cf230017189d4038c0ae0f7388b13f4f7ae1900a155bafb597 AS final
WORKDIR /app
COPY --from=build /app/efbundle ./efbundle
RUN chmod +x ./efbundle
# CONNECTION_STRING is supplied by Compose; the bundle applies all pending migrations then exits.
ENTRYPOINT ["/bin/sh", "-c", "./efbundle --connection \"$CONNECTION_STRING\""]
