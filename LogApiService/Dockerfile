FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["LogApiService/LogApiService.csproj", "LogApiService/"]
RUN dotnet restore "LogApiService/LogApiService.csproj"
COPY LogApiService/ LogApiService/
WORKDIR "/src/LogApiService"
RUN dotnet build "LogApiService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "LogApiService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "LogApiService.dll"]
