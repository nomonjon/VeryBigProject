FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["LogPlatform/LogPlatform.csproj", "LogPlatform/"]
RUN dotnet restore "./LogPlatform/LogPlatform.csproj"
COPY . .
WORKDIR "/src/LogPlatform"
RUN dotnet build "./LogPlatform.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "./LogPlatform.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "LogPlatform.dll"]