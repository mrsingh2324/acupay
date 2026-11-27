using AcuPay.Api.Dtos;
using AcuPay.Api.Middleware;
using AcuPay.Api.Security;
using AcuPay.Data;
using AcuPay.Domain.Interfaces;
using AcuPay.Domain.Rules;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Net;
using System.Text;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------- configuration
builder.Services.Configure<AcuPayOptions>(builder.Configuration.GetSection(AcuPayOptions.SectionName));

// ---------------------------------------------------------------- data access
builder.Services.AddSingleton<DbConnectionFactory>();
builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

// ---------------------------------------------------------------- domain rules
// The rules take their thresholds from configuration, so there is no literal 2.00 in the engine.
builder.Services.AddSingleton(sp =>
{
    AcuPayOptions options = sp.GetRequiredService<IOptions<AcuPayOptions>>().Value;
    return new MatchEngine(options.PriceTolerancePercent, options.AutoApproveLimit);
});
builder.Services.AddSingleton(sp =>
{
    AcuPayOptions options = sp.GetRequiredService<IOptions<AcuPayOptions>>().Value;
    return new ApprovalRules(options.MinReasonLength);
});
builder.Services.AddSingleton<JwtTokenService>();

// ---------------------------------------------------------------- authentication
string jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "Jwt:Key is not configured. Run: dotnet user-secrets set \"Jwt:Key\" \"<32+ random characters>\"");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // Without these, a missing token returns an empty 401 body and a wrong role an empty 403.
        // We want the same error shape everywhere, so write it ourselves.
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                await ErrorHandlingMiddleware.WriteAsync(
                    context.HttpContext, HttpStatusCode.Unauthorized,
                    "UNAUTHENTICATED", "A valid bearer token is required.");
            },
            OnForbidden = async context =>
                await ErrorHandlingMiddleware.WriteAsync(
                    context.HttpContext, HttpStatusCode.Forbidden,
                    "FORBIDDEN_ROLE", "Your role is not allowed to perform this action.")
        };
    });

builder.Services.AddAuthorization();

// ---------------------------------------------------------------- MVC and validation
builder.Services.AddControllers();

// Model validation failures must use the same error shape as everything else,
// not the framework's default ProblemDetails.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        string message = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .SelectMany(entry => entry.Value!.Errors.Select(e => e.ErrorMessage))
            .FirstOrDefault() ?? "The request is not valid.";

        return new BadRequestObjectResult(new ApiErrorResponse
        {
            Error = new ApiError
            {
                Code = "VALIDATION_FAILED",
                Message = message,
                CorrelationId = context.HttpContext.CorrelationId()
            }
        });
    };
});

// ---------------------------------------------------------------- swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "AcuPay API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the token from /api/auth/login. Swagger adds the Bearer prefix."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

WebApplication app = builder.Build();

// ---------------------------------------------------------------- pipeline
// Order matters. Correlation id first so every later log line carries it,
// error handling next so it can catch everything after it.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ErrorHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

/// <summary>Exposed so an integration test project can reference the host.</summary>
public partial class Program { }
