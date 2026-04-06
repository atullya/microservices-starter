using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddEventLog();

// Configure logging levels
builder.Services.Configure<Microsoft.Extensions.Logging.Console.ConsoleLoggerOptions>(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Warning;
});

// Add logging configuration
builder.Host.ConfigureLogging(logging =>
{
    logging.SetMinimumLevel(LogLevel.Information);
    logging.AddConsole(options => options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] ");
    logging.AddDebug();
});

// Add services to the container.
builder.Services.AddOpenApi();

var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["Key"] ?? "fallback_super_secret_key_12345!";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

// Add request logging middleware
app.Use(async (context, next) =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    var requestId = Guid.NewGuid().ToString("N")[..8];
    
    logger.LogInformation("[REQ:{requestId}] {method} {path} from {remoteIp}", 
        requestId, context.Request.Method, context.Request.Path, context.Connection.RemoteIpAddress);
    
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    
    try
    {
        await next.Invoke();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "[REQ:{requestId}] Unhandled exception", requestId);
        throw;
    }
    finally
    {
        stopwatch.Stop();
        logger.LogInformation("[REQ:{requestId}] {method} {path} - {statusCode} in {elapsedMs}ms", 
            requestId, context.Request.Method, context.Request.Path, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
    }
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

// In-Memory User Store
var users = new Dictionary<string, string>(); // username -> password

var logger = app.Services.GetRequiredService<ILogger<Program>>();

// Log service startup
logger.LogInformation("🚀 Auth Service starting up on {timestamp}", DateTime.UtcNow);
logger.LogInformation("📊 Environment: {environment}", app.Environment.EnvironmentName);
logger.LogInformation("🌐 Service URLs: {urls}", string.Join(", ", app.Urls));
logger.LogInformation("👥 Initial user count: {count}", users.Count);

app.MapPost("/auth/register", (UserDto request) =>
{
    var requestId = Guid.NewGuid().ToString("N")[..8];
    logger.LogInformation("[REQ:{requestId}] 👤 User registration attempt: {username}", requestId, request.Username);
    
    try
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            logger.LogWarning("[REQ:{requestId}] ⚠️ Registration failed: username or password is empty", requestId);
            return Results.BadRequest(new { success = false, message = "Username and password are required." });
        }

        if (users.ContainsKey(request.Username))
        {
            logger.LogWarning("[REQ:{requestId}] ⚠️ Registration failed: username '{username}' already exists", requestId, request.Username);
            return Results.Conflict(new { success = false, message = "User already exists." });
        }

        users[request.Username] = request.Password;
        logger.LogInformation("[REQ:{requestId}] ✅ User '{username}' registered successfully", requestId, request.Username);
        logger.LogInformation("👥 Total users after registration: {count}", users.Count);
        
        return Results.Ok(new { success = true, message = "User registered successfully." });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "[REQ:{requestId}] ❌ Error during user registration for '{username}'", requestId, request.Username);
        return Results.Problem("Internal server error during registration");
    }
});

app.MapPost("/auth/login", (UserDto request) =>
{
    var requestId = Guid.NewGuid().ToString("N")[..8];
    logger.LogInformation("[REQ:{requestId}] 🔐 Login attempt: {username}", requestId, request.Username);
    
    try
    {
        if (!users.TryGetValue(request.Username, out var password) || password != request.Password)
        {
            logger.LogWarning("[REQ:{requestId}] ⚠️ Login failed: invalid credentials for '{username}'", requestId, request.Username);
            return Results.Unauthorized();
        }

        // Generate JWT
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, request.Username)
        };

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.Now.AddHours(1),
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        
        logger.LogInformation("[REQ:{requestId}] ✅ User '{username}' logged in successfully", requestId, request.Username);
        logger.LogInformation("🔑 JWT token generated for user '{username}', expires at {expiry}", 
            request.Username, token.ValidTo);

        return Results.Ok(new { success = true, token = tokenString, message = "Login successful" });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "[REQ:{requestId}] ❌ Error during login for '{username}'", requestId, request.Username);
        return Results.Problem("Internal server error during login");
    }
});

app.Run();

// Log service shutdown
logger.LogInformation("🛑 Auth Service shutting down on {timestamp}", DateTime.UtcNow);
logger.LogInformation("👥 Final user count: {count}", users.Count);

public class UserDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
