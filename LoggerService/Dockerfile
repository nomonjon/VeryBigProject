FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["LoggerService/LoggerService.csproj", "LoggerService/"]
RUN dotnet restore "LoggerService/LoggerService.csproj"
COPY . .
WORKDIR "/src/LoggerService"
RUN dotnet build "LoggerService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "LoggerService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "LoggerService.dll"]