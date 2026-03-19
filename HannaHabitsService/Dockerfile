FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8443

FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

RUN dotnet tool install --global dotnet-ef

COPY ["HannaHabitsService/HannaHabitsService.csproj", "./"]
COPY ["HannasHabits.Data.Shared/HannasHabits.Data.Shared.csproj", "../HannasHabits.Data.Shared/"]

RUN dotnet restore "HannaHabitsService.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "HannaHabitsService/HannaHabitsService.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
RUN dotnet publish "HannaHabitsService/HannaHabitsService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HannaHabitsService.dll"]