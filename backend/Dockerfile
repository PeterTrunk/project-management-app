FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder
WORKDIR /src

COPY src/ProjectManager.API/ProjectManager.API.csproj src/ProjectManager.API/
RUN dotnet restore src/ProjectManager.API/ProjectManager.API.csproj

COPY src/ src/
RUN dotnet publish src/ProjectManager.API/ProjectManager.API.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=builder /app/publish .

# Nem rootként futunk: egy esetleges kódfuttatási hiba így nem ad azonnal root
# jogot a konténer névterében. Az 'app' felhasználó az aspnet image része.
# Az 5178-as port 1024 fölött van, tehát nem-rootként is köthető.
USER app

EXPOSE 5178

ENTRYPOINT ["dotnet", "ProjectManager.API.dll"]