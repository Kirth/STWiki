# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY STWiki.csproj .
RUN dotnet restore

# Copy source code and build
COPY . .
RUN dotnet build -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Copy published app
COPY --from=publish /app/publish .

# Expose port
EXPOSE 5000
EXPOSE 5001

# Set environment
ENV ASPNETCORE_URLS=http://+:5000;https://+:5001
ENV ASPNETCORE_ENVIRONMENT=Docker

# Entry point
ENTRYPOINT ["dotnet", "STWiki.dll"]

# Development stage with SDK and EF tools
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS development
WORKDIR /src

# Install EF Core CLI tools globally and make them available
RUN dotnet tool install --global dotnet-ef --version 8.0.0
ENV PATH="${PATH}:/root/.dotnet/tools"

# Verify EF tools are working
RUN dotnet ef --version

# Copy project file and restore dependencies first (for better caching)
COPY *.csproj ./
RUN dotnet restore

# Copy source code
COPY . .

# Set environment for development
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Docker

# Entry point for development - force URL override
ENTRYPOINT ["dotnet", "run", "--urls", "http://+:5000"]