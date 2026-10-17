# Build from the repository root:  docker build -f src/LinkedArtNet.Dates.Service/Dockerfile -t linked-art-net-dates .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props linked-art-net.slnx ./
COPY src/LinkedArtNet/LinkedArtNet.csproj src/LinkedArtNet/
COPY src/LinkedArtNet.Dates/LinkedArtNet.Dates.csproj src/LinkedArtNet.Dates/
COPY src/LinkedArtNet.Dates.Service/LinkedArtNet.Dates.Service.csproj src/LinkedArtNet.Dates.Service/
RUN dotnet restore src/LinkedArtNet.Dates.Service/LinkedArtNet.Dates.Service.csproj
COPY src/LinkedArtNet src/LinkedArtNet
COPY src/LinkedArtNet.Dates src/LinkedArtNet.Dates
COPY src/LinkedArtNet.Dates.Service src/LinkedArtNet.Dates.Service
ARG GIT_SHA=unknown
RUN dotnet publish src/LinkedArtNet.Dates.Service/LinkedArtNet.Dates.Service.csproj -c Release -o /app --no-restore \
    -p:SourceRevisionId=$GIT_SHA -p:ContinuousIntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build /app .
COPY profiles /app/profiles
COPY periods /app/periods
ENV ASPNETCORE_HTTP_PORTS=8080 \
    Parser__ProfilesDirectory=/app/profiles \
    DOTNET_gcServer=0
EXPOSE 8080
USER $APP_UID
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s CMD ["dotnet", "LinkedArtNet.Dates.Service.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "LinkedArtNet.Dates.Service.dll"]
