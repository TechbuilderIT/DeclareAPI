using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Techbuilder.DeclareAPI.Dapper;
using Techbuilder.DeclareAPI.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Get connection string from configuration or environment
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("DB_CONNECTION")
    ?? "Host=localhost;Port=5432;Database=declareapi_sample;Username=postgres;Password=postgres";

// Add Authentication (JWT Bearer for demo)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "DeclareAPI.Sample",
            ValidAudience = "DeclareAPI.Sample",
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes("YourSuperSecretKeyThatIsAtLeast32Characters!"))
        };
    });

// Add Authorization Policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("admin_only", policy =>
        policy.RequireRole("admin"));

    options.AddPolicy("doctor_or_admin", policy =>
        policy.RequireRole("admin", "doctor"));
});

// Add DeclareAPI services
builder.Services.AddDeclareApi(options =>
{
    options.ConfigFile = "declareapi.yaml";
    options.UseDataAccess(sp => new DapperDataAccess(connectionString, DatabaseProvider.PostgreSQL));
    options.ScanHandlersFrom<Program>();

    // Observability (all enabled by default)
    options.EnableCorrelationId = true;
    options.EnableRequestLogging = true;
    options.EnableHealthChecks = true;

    // Policies (all enabled by default)
    options.EnableRateLimiting = true;
    options.EnableCaching = true;
});

// Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "DeclareAPI Sample", Version = "v1" });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "DeclareAPI Sample v1"));
}

// Add observability middleware (correlation ID, logging)
app.UseDeclareApiObservability();

// Add authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

// Add rate limiting and caching
app.UseDeclareApiPolicies();

// Map DeclareAPI endpoints from YAML configuration
app.MapDeclareApi();

// Map health check endpoints
app.MapDeclareApiHealthChecks();

app.Run();
