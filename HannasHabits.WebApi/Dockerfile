# Build context is the repository root (see docker-compose.yml):
#   docker build -f HannasHabits.WebApi/Dockerfile -t hannas-habits-api .

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first, with the project files only: this layer stays cached until a package reference changes.
COPY HannasHabits.Domain/HannasHabits.Domain.csproj HannasHabits.Domain/
COPY HannasHabits.Application/HannasHabits.Application.csproj HannasHabits.Application/
COPY HannasHabits.Infrastructure/HannasHabits.Infrastructure.csproj HannasHabits.Infrastructure/
COPY HannasHabits.WebApi/HannasHabits.WebApi.csproj HannasHabits.WebApi/
RUN dotnet restore HannasHabits.WebApi/HannasHabits.WebApi.csproj

COPY HannasHabits.Domain/ HannasHabits.Domain/
COPY HannasHabits.Application/ HannasHabits.Application/
COPY HannasHabits.Infrastructure/ HannasHabits.Infrastructure/
COPY HannasHabits.WebApi/ HannasHabits.WebApi/
RUN dotnet publish HannasHabits.WebApi/HannasHabits.WebApi.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# The .NET 8 images already listen on port 8080 (ASPNETCORE_HTTP_PORTS) and ship an unprivileged user.
# TLS is not terminated here: put a reverse proxy in front for HTTPS.
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "HannasHabits.WebApi.dll"]
