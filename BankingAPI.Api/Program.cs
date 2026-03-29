using AspNetCoreRateLimit;
using BankingAPI.Api.Extensions;
using BankingAPI.Api.Filters;
using BankingAPI.Application.DTOs.Errors;
using BankingAPI.Application.Interfaces;
using BankingAPI.Application.Services;
using BankingAPI.Infrastructure.Data;
using BankingAPI.Infrastructure.Middleware;
using BankingAPI.Infrastructure.Services;
using BankingAPI.Infrastructure.Services.Validators;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using System.Threading.RateLimiting;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithCorrelationId()
            .Enrich.WithProperty("Application", "BankingAPI")
            .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}",
                restrictedToMinimumLevel: LogEventLevel.Information)
            .WriteTo.File(
                new JsonFormatter(),
                "logs/audit-.json",
                rollingInterval: RollingInterval.Day,
                restrictedToMinimumLevel: LogEventLevel.Information)
            .WriteTo.File(
                "logs/error-.txt",
                rollingInterval: RollingInterval.Day,
                restrictedToMinimumLevel: LogEventLevel.Error)
            .CreateLogger();

        builder.Host.UseSerilog();

        // Add services
        builder.Services.AddControllers(options =>
        {
            options.Filters.Add<IdempotencyFilter>();
            options.Filters.Add<GlobalExceptionFilter>();
        })
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
            options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        });

        builder.Services.AddEndpointsApiExplorer();

        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Banking API",
                Version = "v1",
                Description = "A comprehensive banking API with clean architecture, caching, and security",
                Contact = new OpenApiContact
                {
                    Name = "Banking API Support",
                    Email = "aduwujoseph@gmail.com"
                },
                License = new OpenApiLicense
                {
                    Name = "MIT License",
                    Url = new Uri("https://opensource.org/licenses/MIT")
                }
            });

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        builder.Services.AddRateLimiter(options =>
        {
            options.AddPolicy("TransferPolicy", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString(),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 2
                    }
                )
            );

            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.ContentType = "application/json";

                var response = new ErrorResponse
                {
                    Message = "Too many requests. Please try again later.",
                    Code = "RATE_LIMIT_EXCEEDED",
                    Retryable = true
                };

                await context.HttpContext.Response.WriteAsJsonAsync(response, token);
            };
        });

        // Database Configuration - MySQL
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        builder.Services.AddDbContext<BankingDbContext>(options =>
        {
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
                mysqlOptions =>
                {
                    mysqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                    mysqlOptions.CommandTimeout(60);
                });
            options.EnableSensitiveDataLogging(builder.Environment.IsDevelopment());
            options.EnableDetailedErrors(builder.Environment.IsDevelopment());
        });

        // Caching Configuration
        builder.Services.AddMemoryCache(); // L1 cache

        // Distributed Cache - In-memory for development, Redis for production

        builder.Services.AddDistributedMemoryCache();

        // Response Caching
        builder.Services.AddResponseCaching();

        // Application Services Registration

        // DbContext
        builder.Services.AddScoped<IBankingDbContext, BankingDbContext>();

        // Core Services
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IAccountService, AccountService>();
        builder.Services.AddScoped<IAccountLedgerService, AccountLedgerService>();
        builder.Services.AddScoped<ITransferService, TransferService>();
        builder.Services.AddScoped<ITransactionService, TransactionService>();
        builder.Services.AddScoped<ITransactionValidator, TransactionValidator>();

        // Infrastructure Services
        builder.Services.AddScoped<IAuditService, AuditService>();
        //builder.Services.AddScoped<IAntiFraudService, MockAntiFraudService>();
        builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
        builder.Services.AddScoped<IIdempotencyService, IdempotencyService>();
        builder.Services.AddScoped<CurrentUserService>();
        builder.Services.AddHttpContextAccessor();

        // Configure JWT Authentication
        builder.Services.AddJwtAuthentication(builder.Configuration);

        // Configure Rate Limiting
        builder.Services.AddRateLimiting(builder.Configuration);

        // Configure CORS
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        var app = builder.Build();

        // Configure the HTTP request pipeline
        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Banking API v1");
                c.RoutePrefix = "swagger";  // This makes Swagger UI available at /swagger
            });
        }

        app.UseHttpsRedirection();
        app.UseCors("AllowAll");

        // Important: Static files might be needed for Swagger
        app.UseStaticFiles();

        // Custom middleware 
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<GlobalExceptionHandler>();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseIpRateLimiting();

        app.MapControllers();

        // Ensure database is created
        using (var scope = app.Services.CreateScope())
        {
            try
            {
                var context = scope.ServiceProvider.GetRequiredService<BankingDbContext>();

                // Apply migrations first (important)
                await context.Database.MigrateAsync();

                // Seed data
                await DatabaseSeeder.SeedAsync(context);
            }
            catch (Exception ex)
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "An error occurred while seeding the database.");
            }
        }

        try
        {
            Log.Information("Starting up the application");
            app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application start-up failed");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}