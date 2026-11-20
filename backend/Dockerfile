# syntax=docker/dockerfile:1

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from project files only, so the layer is cached until dependencies change.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Injaz.Domain/Injaz.Domain.csproj                 src/Injaz.Domain/
COPY src/Injaz.Application/Injaz.Application.csproj       src/Injaz.Application/
COPY src/Injaz.Infrastructure/Injaz.Infrastructure.csproj src/Injaz.Infrastructure/
COPY src/Injaz.Api/Injaz.Api.csproj                       src/Injaz.Api/
RUN dotnet restore src/Injaz.Api/Injaz.Api.csproj

COPY src/ src/
RUN dotnet publish src/Injaz.Api/Injaz.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

# ---------- runtime ----------
# Chiseled = distroless Ubuntu, runs as non-root "app" user by default.
# "-extra" adds ICU + tzdata (time zones for due dates, culture-aware formatting).
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS runtime
WORKDIR /app
# Lets the deploy prune only old Injaz images on a server shared with other projects.
LABEL org.opencontainers.image.source="https://github.com/MrADAM02/injaz"
COPY --from=build /app .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Injaz.Api.dll"]
