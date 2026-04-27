using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;
using System.Text.Json.Serialization;
using ChessAPI.Data;
// using ChessAPI.Hubs;
// using ChessAPI.Middleware;
// using ChessAPI.Services.Implementations;
// using ChessAPI.Services.Interfaces;
using Microsoft.AspNetCore.ResponseCompression;
using Npgsql;
using Serilog;
using System.Threading.RateLimiting;
using ChessAPI.Middleware;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using HealthChecks.UI.Client;
using ChessAPI.Services.Interfaces;
using ChessAPI.Services.Implementations;
using ChessAPI.Hubs;
using ChessAPI.Helpers;
using ChessAPI.Services.BackgroundServices;

using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// ==================== Serilog Configuration ====================
builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithThreadId()
        .WriteTo.Console()
        .WriteTo.File("Logs/chess-api-.txt", rollingInterval: RollingInterval.Day);
});

// ==================== Add Services ====================

// Database Context - PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptionsAction: npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorCodesToAdd: null);
            npgsqlOptions.CommandTimeout(30);
        }
    )
);

// Add Npgsql DataSource for raw SQL operations
builder.Services.AddSingleton<NpgsqlDataSource>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
    dataSourceBuilder.UseJsonNet();
    return dataSourceBuilder.Build();
});

// Redis for distributed caching and SignalR backplane
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "ChessAPI_";
});

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero
    };

    // Enable JWT for SignalR
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
});

// Authorization Policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminRole", policy => policy.RequireRole("Admin"));
    options.AddPolicy("RequireUserRole", policy => policy.RequireRole("User", "Admin"));
});

// CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:5173" })
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// SignalR with Redis backplane for scaling
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 1024 * 64; // 64KB
    options.StreamBufferCapacity = 10;
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.KeepAliveInterval = TimeSpan.FromSeconds(30);
    options.HandshakeTimeout = TimeSpan.FromSeconds(30);
})
.AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.PropertyNamingPolicy = null;
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
})
.AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis")!, options =>
{
    options.Configuration.ChannelPrefix = "ChessSignalR";
});

// Response Compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/json", "text/plain", "text/json" }
    );
});

// Controllers with JSON options
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });

// Memory Cache
builder.Services.AddMemoryCache();

// Health Checks with PostgreSQL
// builder.Services.AddHealthChecks()
//     .AddNpgSql(
//         NpgsqlConnectionStringBuilder: builder.Configuration.GetConnectionString("DefaultConnection")!,
//         healthQuery: "SELECT 1;",
//         name: "PostgreSQL",
//         // failureStatus: HealthStatus.Unhealthy,
//         tags: new[] { "db", "sql", "postgresql" }
//     )
//     .AddRedis(builder.Configuration.GetConnectionString("Redis")!, "Redis");


builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    .AddNpgSql(
        builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "PostgreSQL",
        tags: new[] { "ready" })
    .AddRedis(
        builder.Configuration.GetConnectionString("Redis")!,
        name: "Redis",
        tags: new[] { "ready" });

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Chess API",
        Version = "v1",
        Description = "Real-time chess game API with SignalR and PostgreSQL",
        Contact = new OpenApiContact
        {
            Name = "Chess API Support",
            Email = "support@chessapi.com"
        }
    });

    // JWT Authentication in Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. \r\n\r\n" +
                      "Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\n" +
                      "Example: 'Bearer 12345abcdef'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    // c.AddSecurityRequirement(new OpenApiSecurityRequirement
    // {
    //     {
    //         new OpenApiSecurityScheme
    //         {
    //             Reference = new OpenApiReference
    //             {
    //                 Type = ReferenceType.SecurityScheme,
    //                 Id = "Bearer"
    //             },
    //             Scheme = "oauth2",
    //             Name = "Bearer",
    //             In = ParameterLocation.Header
    //         },
    //         new List<string>()
    //     }
    // });

    // Include XML Comments
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Register Application Services
RegisterServices(builder.Services);

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
        httpContext => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        await context.HttpContext.Response.WriteAsync("Too many requests. Please try again later.", token);
    };
});

// Output caching
builder.Services.AddOutputCache(options =>
{
    options.DefaultExpirationTimeSpan = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

// ==================== Configure Pipeline ====================

// Global Exception Handling
// app.UseMiddleware<ExceptionMiddleware>();

// Serilog request logging
app.UseSerilogRequestLogging();

// Swagger (always available in development, optionally in production)
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("EnableSwagger"))
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Chess API V1");
        c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
        c.DisplayRequestDuration();
    });
}

// HTTPS Redirection (except for development)
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}

// Response Compression
app.UseResponseCompression();

// Static Files (for client app if serving from same host)
app.UseDefaultFiles();
app.UseStaticFiles();

// CORS
app.UseCors("CorsPolicy");

// Rate Limiting
app.UseRateLimiter();

// Output Caching
app.UseOutputCache();

// Custom JWT Middleware
app.UseMiddleware<ExceptionMiddleware>();

// 2. Security headers
app.UseMiddleware<SecurityHeadersMiddleware>();


// 4. Swagger (only in development)
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("EnableSwagger"))
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Chess API V1");
        c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
    });
}

// 5. HTTPS Redirection
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// 6. Request logging
app.UseMiddleware<RequestLoggingMiddleware>();

// 7. Response Compression
app.UseResponseCompression();

// 8. Static Files
app.UseDefaultFiles();
app.UseStaticFiles();

// 9. CORS
app.UseMiddleware<CorsMiddleware>();

// 10. Rate Limiting
app.UseMiddleware<RateLimitingMiddleware>();

// 12. Custom JWT Middleware (after auth but before endpoints)
app.UseMiddleware<JwtMiddleware>();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();


// Map Endpoints
app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        
        var response = new
        {
            Status = report.Status.ToString(),
            Duration = report.TotalDuration,
            Checks = report.Entries.Select(e => new
            {
                Name = e.Key,
                Status = e.Value.Status.ToString(),
                Description = e.Value.Description,
                Duration = e.Value.Duration
            })
        };
        
        await context.Response.WriteAsJsonAsync(response);
    }
});

// Map SignalR Hubs
app.MapHub<ChessHub>("/hubs/chess", options =>
{
    options.TransportMaxBufferSize = 1024 * 64;
    options.ApplicationMaxBufferSize = 1024 * 64;
    options.TransportSendTimeout = TimeSpan.FromSeconds(30);
    options.LongPolling.PollTimeout = TimeSpan.FromSeconds(90);
    options.WebSockets.CloseTimeout = TimeSpan.FromSeconds(5);
});

app.MapHub<ChatHub>("/hubs/chat");

// SPA Fallback (if serving React/Vue app)
app.MapFallbackToFile("index.html");

// Ensure database is created and migrations applied
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        // Apply migrations
        await dbContext.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied successfully");

        // Seed initial data
        await SeedData.InitializeAsync(scope.ServiceProvider);
        logger.LogInformation("Database seeded successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while migrating or seeding the database");
        throw;
    }
}

app.Run();

// ==================== Service Registration ====================
static void RegisterServices(IServiceCollection services)
{
    // Scoped Services (per request)
    services.AddScoped<IAuthService, AuthService>();
    services.AddScoped<IGameService, GameService>();
    services.AddScoped<IChessEngine, ChessEngine>();

    // Singleton Services
    services.AddSingleton<IMatchmakingService, MatchmakingService>();
    services.AddSingleton<IConnectionManager, ConnectionManager>();

    // Transient Services
    services.AddTransient<IJwtHelper, JwtHelper>();
    services.AddTransient<IPasswordHasher, PasswordHasher>();
    services.AddTransient<IRatingHelper, RatingHelper>();
    services.AddTransient<IValidationHelper, ValidationHelper>();
    // Background Services
    services.AddHostedService<GameCleanupService>();
    services.AddHostedService<MatchmakingBackgroundService>();
}

// ==================== Seed Data ====================
public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        // Only seed if database is empty
        if (!await context.Users.AnyAsync())
        {
            logger.LogInformation("Seeding initial data...");

            // Add default admin user
            var adminUser = new User
            {
                Id = Guid.NewGuid(),
                Email = "admin@chess.com",
                Username = "Admin",
                PasswordHash = passwordHasher.HashPassword("Admin123!"),
                Role = "Admin",
                Rating = 2000,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            // Add demo user
            var demoUser = new User
            {
                Id = Guid.NewGuid(),
                Email = "demo@chess.com",
                Username = "DemoPlayer",
                PasswordHash = passwordHasher.HashPassword("Demo123!"),
                Role = "User",
                Rating = 1500,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            // Add bot user for AI games
            var botUser = new User
            {
                Id = Guid.NewGuid(),
                Email = "bot@chess.com",
                Username = "Stockfish",
                PasswordHash = passwordHasher.HashPassword(Guid.NewGuid().ToString()),
                Role = "Bot",
                Rating = 2000,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            await context.Users.AddRangeAsync(adminUser, demoUser, botUser);
            await context.SaveChangesAsync();

            logger.LogInformation("Seeded {Count} users", 3);
        }
    }
}