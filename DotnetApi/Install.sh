#!/bin/bash

# Exit on error
set -e

echo "Installing NuGet packages..."

dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package Microsoft.AspNetCore.SignalR.StackExchangeRedis

dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add package Npgsql.Json.NET
dotnet add package Microsoft.EntityFrameworkCore.Tools

dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis
dotnet add package AspNetCore.HealthChecks.Redis

dotnet add package Swashbuckle.AspNetCore

dotnet add package AspNetCore.HealthChecks.NpgSql
dotnet add package AspNetCore.HealthChecks.UI.Client

dotnet add package BCrypt.Net-Next
dotnet add package Chess
dotnet add package System.Linq.Dynamic.Core

dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Enrichers.Environment
dotnet add package Serilog.Enrichers.Thread
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Sinks.File

echo "All packages installed successfully."