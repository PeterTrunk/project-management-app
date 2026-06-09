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

EXPOSE 5178

ENTRYPOINT ["dotnet", "ProjectManager.API.dll"]