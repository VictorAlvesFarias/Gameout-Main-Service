# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

# Project files first: the restore layer is reused while only the code changes.
COPY ["Api/Api.csproj", "Api/"]
COPY ["Application/Application.csproj", "Application/"]
COPY ["Domain/Domain.csproj", "Domain/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]

# Web.Api.Toolkit.* come from the local feed that nuget.config points at
# (../.NET-Web-Api-Toolkit/bin/Packages). The build receives that folder as the "web-api-toolkit"
# context and puts it at the same relative path, so nuget.config works unchanged:
#   docker build --build-context web-api-toolkit=../.NET-Web-Api-Toolkit/bin/Packages .
# (Compose passes it through additional_contexts.)
COPY ["nuget.config", "./"]
COPY --from=web-api-toolkit . /.NET-Web-Api-Toolkit/bin/Packages/
RUN dotnet restore "Api/Api.csproj"

COPY . .
RUN dotnet publish "Api/Api.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

RUN groupadd --system app && useradd --system --gid app --no-create-home app
COPY --from=build /app/publish .
USER app

ENTRYPOINT ["dotnet", "Api.dll"]
