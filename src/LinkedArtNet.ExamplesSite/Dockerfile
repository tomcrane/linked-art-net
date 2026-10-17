# Build from the repository root:  docker build -f src/LinkedArtNet.ExamplesSite/Dockerfile -t linked-art-net-site .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props linked-art-net.slnx ./
COPY src/LinkedArtNet/LinkedArtNet.csproj src/LinkedArtNet/
COPY src/LinkedArtNet.Examples/LinkedArtNet.Examples.csproj src/LinkedArtNet.Examples/
COPY src/LinkedArtNet.Dates/LinkedArtNet.Dates.csproj src/LinkedArtNet.Dates/
COPY src/LinkedArtNet.Dates.Corpus/LinkedArtNet.Dates.Corpus.csproj src/LinkedArtNet.Dates.Corpus/
COPY src/LinkedArtNet.Dates.Llm/LinkedArtNet.Dates.Llm.csproj src/LinkedArtNet.Dates.Llm/
COPY src/LinkedArtNet.ExamplesSite/LinkedArtNet.ExamplesSite.csproj src/LinkedArtNet.ExamplesSite/
RUN dotnet restore src/LinkedArtNet.ExamplesSite/LinkedArtNet.ExamplesSite.csproj
COPY src/LinkedArtNet src/LinkedArtNet
COPY src/LinkedArtNet.Examples src/LinkedArtNet.Examples
COPY src/LinkedArtNet.Dates src/LinkedArtNet.Dates
COPY src/LinkedArtNet.Dates.Corpus src/LinkedArtNet.Dates.Corpus
COPY src/LinkedArtNet.Dates.Llm src/LinkedArtNet.Dates.Llm
COPY docs/canonical-expression.md docs/
COPY src/LinkedArtNet.ExamplesSite src/LinkedArtNet.ExamplesSite
ARG GIT_SHA=unknown
RUN dotnet publish src/LinkedArtNet.ExamplesSite/LinkedArtNet.ExamplesSite.csproj -c Release -o /app --no-restore \
    -p:SourceRevisionId=$GIT_SHA -p:ContinuousIntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build /app .
# The site reads the manifest, cached site JSON, example source, profiles and corpus from the repository layout.
COPY examples /repo/examples
COPY profiles /repo/profiles
COPY periods /repo/periods
COPY corpus /repo/corpus
ENV ASPNETCORE_HTTP_PORTS=8080 \
    Examples__RepoRoot=/repo \
    TimeSpanService__BaseUrl=
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "LinkedArtNet.ExamplesSite.dll"]
