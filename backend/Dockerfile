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

# A healthcheckhez HTTP klienst kell telepiteni: az aspnet image Debian-alapu es
# szandekosan nem tartalmaz sem curl-t, sem wget-et. A /dev/tcp trukk sem jarhato,
# mert az a bash sajatossaga, a kepben viszont dash (sh) van.
#
# A --no-install-recommends es a lista-torles azert kell, hogy a nem-root futtatas
# melle a tamadasi felulet se nojon tobbet a szuksegesnel.
#
# FIGYELEM: a Traefik oldali health check NEM ezt hasznalja - azt a Traefik vegzi
# kivulrol, kontener-eszkoz nelkul. A curl a Docker-szintu healthcheckhez kell,
# ami a telepito feluleten (Dokploy) jelzi a kontener allapotat.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=builder /app/publish .

# Nem rootként futunk: egy esetleges kódfuttatási hiba így nem ad azonnal root
# jogot a konténer névterében. Az 'app' felhasználó az aspnet image része.
# Az 5178-as port 1024 fölött van, tehát nem-rootként is köthető.
USER app

EXPOSE 5178

ENTRYPOINT ["dotnet", "ProjectManager.API.dll"]
