# Multi-stage Dockerfile for DentalCare SaaS (.NET 10)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy solution and project definitions
COPY DentalSaaS.slnx ./
COPY src/DentalSaaS.Domain/DentalSaaS.Domain.csproj src/DentalSaaS.Domain/
COPY src/DentalSaaS.Application/DentalSaaS.Application.csproj src/DentalSaaS.Application/
COPY src/DentalSaaS.Infrastructure/DentalSaaS.Infrastructure.csproj src/DentalSaaS.Infrastructure/
COPY src/DentalSaaS.Web/DentalSaaS.Web.csproj src/DentalSaaS.Web/
COPY tests/DentalSaaS.Tests/DentalSaaS.Tests.csproj tests/DentalSaaS.Tests/

# Restore dependencies
RUN dotnet restore src/DentalSaaS.Web/DentalSaaS.Web.csproj

# Copy full source and publish
COPY . ./
RUN dotnet publish src/DentalSaaS.Web/DentalSaaS.Web.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5005
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5005

ENTRYPOINT ["dotnet", "DentalSaaS.Web.dll"]
