# Stage 1: Base runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
# Configure Kestrel to listen on port 8080 (default for non-HTTPS)
ENV ASPNETCORE_HTTP_PORTS=8080

# Stage 2: Build environment
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first to leverage Docker caching
# (restore will only run if these .csproj files change)
COPY ["PaymentService/PaymentService.csproj", "PaymentService/"]
COPY ["Application/Application.csproj", "Application/"]
COPY ["Domain/Domain.csproj", "Domain/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]

# Restore dependencies for the main service
RUN dotnet restore "PaymentService/PaymentService.csproj"

# Copy all source code (after restore to avoid invalidating cache)
COPY . .

# Build the project in Release configuration
WORKDIR "/src/PaymentService"
RUN dotnet build "PaymentService.csproj" -c Release -o /app/build

# Stage 3: Publish the application (ready for deployment)
FROM build AS publish
RUN dotnet publish "PaymentService.csproj" -c Release -o /app/publish

# Stage 4: Final runtime image (lightweight)
FROM base AS final
WORKDIR /app
# Copy published output from the publish stage
COPY --from=publish /app/publish .
# Set the entry point
ENTRYPOINT ["dotnet", "PaymentService.dll"]