# Base stage: only used by Visual Studio's container debugging ("fast mode"), which runs the normal JIT build
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

# Native AOT compiles for the machine it runs on, so this stage is NOT cross-compiled:
# it builds amd64 on your PC and arm64 on GitHub's ARM runner
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble-aot AS build
ARG BUILD_CONFIGURATION=Release
ARG TARGETARCH
WORKDIR /src
COPY ["SpaceAffinityApi/SpaceAffinityApi.csproj", "SpaceAffinityApi/"]
# If you later add class library projects, add a `COPY` line for each one's `.csproj` before the restore step, and one for each one's folder after it.
RUN dotnet restore "./SpaceAffinityApi/SpaceAffinityApi.csproj" -a $TARGETARCH
COPY SpaceAffinityApi/ SpaceAffinityApi/
WORKDIR "/src/SpaceAffinityApi"

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG TARGETARCH
RUN dotnet publish "./SpaceAffinityApi.csproj" -c $BUILD_CONFIGURATION -a $TARGETARCH -o /app/publish --no-restore \
    && rm -f /app/publish/*.dbg /app/publish/*.pdb

# Chiseled runtime-deps: no .NET runtime, shell or package manager; runs as non-root and listens on 8080 by default
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 8080
ENTRYPOINT ["./SpaceAffinityApi"]
