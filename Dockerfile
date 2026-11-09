# Salė+ API container (used for hosting, e.g. on Render).
# Build context: the repository root.  Build locally with:  docker build -t saleplius-api .
# Secrets are NOT baked into the image: Jwt__Key and the Firebase key come from the host's environment.

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore first, so the package layer is cached when only code changes
COPY SalePlius/SalePlius.Shared/SalePlius.Shared.csproj SalePlius/SalePlius.Shared/
COPY SalePlius/SalePlus.Api/SalePlius.Api.csproj SalePlius/SalePlus.Api/
RUN dotnet restore SalePlius/SalePlus.Api/SalePlius.Api.csproj

COPY SalePlius/SalePlius.Shared/ SalePlius/SalePlius.Shared/
COPY SalePlius/SalePlus.Api/ SalePlius/SalePlus.Api/
RUN dotnet publish SalePlius/SalePlus.Api/SalePlius.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_ENVIRONMENT=Production
# Runs as the image's built-in non-root user
USER $APP_UID

# Render passes the port to listen on in $PORT; 8080 when run anywhere else.
EXPOSE 8080
CMD ["sh", "-c", "ASPNETCORE_HTTP_PORTS=${PORT:-8080} exec dotnet SalePlius.Api.dll"]
