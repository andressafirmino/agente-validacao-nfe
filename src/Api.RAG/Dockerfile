FROM mcr.microsoft.com/dotnet/sdk:10.0.403 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Api.RAG/Api.RAG.csproj src/Api.RAG/
RUN dotnet restore src/Api.RAG/Api.RAG.csproj

COPY src/Api.RAG/ src/Api.RAG/
RUN dotnet publish src/Api.RAG/Api.RAG.csproj -c Release -o /app/publish --no-restore -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build --chown=$APP_UID /app/publish .

USER $APP_UID
ENTRYPOINT ["dotnet", "Api.RAG.dll"]
