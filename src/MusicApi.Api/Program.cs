// Punto de entrada de MusicApi (backend de TONYX).
// En tareas posteriores añadiremos aquí: CORS, rate limiting,
// manejo centralizado de excepciones, etc.

using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using MusicApi.Api.Extensions;
using MusicApi.Application.Auth;
using MusicApi.Application.Interfaces;
using MusicApi.Application.Settings;
using MusicApi.Infrastructure;
using Serilog;
using Serilog.Events;

// Logger de arranque: captura errores que ocurran antes de que
// la aplicación termine de configurarse.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Iniciando MusicApi (TONYX)");

    var builder = WebApplication.CreateBuilder(args);

    // Logger definitivo: consola + archivo diario (se conservan 14 días).
    builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            path: "logs/musicapi-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14));

    // Base de datos: la cadena de conexión viene de user-secrets (nunca del código).
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "Falta ConnectionStrings:DefaultConnection. Configúrala con 'dotnet user-secrets set'.");
    }

    // JWT: Issuer y Audience vienen de appsettings; la clave de firma, de user-secrets.
    var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                     ?? new JwtOptions();
    jwtOptions.Validate();

    builder.Services.AddInfrastructure(connectionString, jwtOptions);

    // Validadores de las solicitudes de autenticación.
    builder.Services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();
    builder.Services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
    builder.Services.AddScoped<IValidator<RefreshRequest>, RefreshRequestValidator>();

    // Autenticación: la API valida el JWT de cada petición protegida.
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            // Los claims conservan su nombre original ("sub", "sid").
            options.MapInboundClaims = false;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(jwtOptions.GetSigningKeyBytes()),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            options.Events = new JwtBearerEvents
            {
                // Además de la firma, la sesión debe seguir activa en la base de datos.
                // Así cerrar una sesión desde otro dispositivo surte efecto al instante.
                OnTokenValidated = async context =>
                {
                    var userId = context.Principal?.GetUserId();
                    var sessionId = context.Principal?.GetSessionId();

                    if (userId is null || sessionId is null)
                    {
                        context.Fail("El token no identifica usuario y sesión.");
                        return;
                    }

                    var account = context.HttpContext.RequestServices.GetRequiredService<IAccountService>();
                    var active = await account.IsSessionActiveAsync(
                        userId.Value,
                        sessionId.Value,
                        context.HttpContext.RequestAborted);

                    if (!active)
                    {
                        context.Fail("La sesión no está activa.");
                    }
                }
            };
        });

    builder.Services.AddAuthorization();

    // Habilita el uso de controladores. Los enumerados se escriben con su nombre
    // ("Android", "Windows"...) y no distinguen mayúsculas.
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    // Swagger / OpenAPI (Swashbuckle), con el botón Authorize para probar endpoints protegidos.
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "TONYX · MusicApi",
            Version = "v1",
            Description = "API REST del backend de TONYX."
        });

        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Name = "Authorization",
            Description = "Pega solo el accessToken. Swagger añade el prefijo 'Bearer '."
        });

        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });
    });

    var app = builder.Build();

    // Registra una línea de log por cada petición HTTP.
    app.UseSerilogRequestLogging();

    // Swagger solo en desarrollo.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "MusicApi v1");
            options.DocumentTitle = "TONYX · MusicApi";
        });
    }

    app.UseHttpsRedirection();

    // El orden importa: primero se identifica al usuario, luego se decide qué puede hacer.
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "MusicApi terminó de forma inesperada");
}
finally
{
    Log.CloseAndFlush();
}