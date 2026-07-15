using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using Xavissa.Backend.Host.DependencyInjection;
using Xavissa.Backend.Security;
using Xavissa.Backend.Services;
using Xavissa.Database;
using Xavissa.Database.Security;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Is(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development"
        ? LogEventLevel.Information
        : LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.File("Logs/xavissa-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi

//bdContect
builder.Services.AddDbContext<XavissaDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Supabase"),
        npgsqlOptions =>
        {
            npgsqlOptions.CommandTimeout(120);
            npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
        }
    ).ConfigureWarnings(warnings =>
        warnings.Ignore(RelationalEventId.PendingModelChangesWarning)
    )
);

//
builder
    .Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System
            .Text
            .Json
            .Serialization
            .ReferenceHandler
            .IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = builder.Environment.IsDevelopment();
    });
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IRequestContext, HttpRequestContext>();
builder.Services.AddScoped<TenantAccessService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IRlsContextService, RlsContextService>();
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            In = ParameterLocation.Header,
            Description = "Please enter token in the format: Bearer {your token}",
            Name = "Authorization",
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer",
        }
    );
    c.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer",
                    },
                },
                new string[] { }
            },
        }
    );
});
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
{
    Log.Warning("Missing Jwt:Key in configuration; using fallback key.");
    jwtKey = "temporary_fallback_secret";
}

builder.Services.AddBackendModules();

//JWT Authentication
builder
    .Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.NameIdentifier,
        };
    });

var app = builder.Build();

if (args.Length > 0 && string.Equals(args[0], "seed-system-admin", StringComparison.OrdinalIgnoreCase))
{
    static string? ReadOption(string[] values, string name)
    {
        var index = Array.FindIndex(values, x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < values.Length ? values[index + 1] : null;
    }

    await using var scope = app.Services.CreateAsyncScope();
    var seedService = scope.ServiceProvider.GetRequiredService<SystemAdminSeedService>();
    var result = await seedService.SeedAsync(
        ReadOption(args, "--username") ?? "admin",
        ReadOption(args, "--email") ?? "admin@xavissa.local",
        ReadOption(args, "--password") ?? throw new InvalidOperationException("--password is required."),
        args.Any(x => string.Equals(x, "--overwrite", StringComparison.OrdinalIgnoreCase)));
    Console.WriteLine(result);
    return;
}

// Do not run remote PostgreSQL migrations on normal app startup.
// Apply migrations explicitly during deployment or a maintenance workflow.

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseMiddleware<RlsContextMiddleware>();
app.UseAuthorization();

// app.UseHttpsRedirection();
app.MapControllers();

var summaries = new[]
{
    "Freezing",
    "Bracing",
    "Chilly",
    "Cool",
    "Mild",
    "Warm",
    "Balmy",
    "Hot",
    "Sweltering",
    "Scorching",
};

if (app.Environment.IsDevelopment())
{
    app.MapGet(
            "/weatherforecast",
            () =>
            {
                var forecast = Enumerable
                    .Range(1, 5)
                    .Select(index => new WeatherForecast(
                        DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                        Random.Shared.Next(-20, 55),
                        summaries[Random.Shared.Next(summaries.Length)]
                    ))
                    .ToArray();
                return forecast;
            }
        )
        .WithName("GetWeatherForecast");
}

app.Urls.Clear();
app.Urls.Add("http://localhost:5087");
if (app.Environment.IsDevelopment())
{
    Log.Information("Backend starting on http://localhost:5087");
    Log.Information("Config file location: {ConfigBaseDirectory}", AppContext.BaseDirectory);
    Log.Information("Backend working directory: {WorkingDirectory}", Directory.GetCurrentDirectory());
}

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
