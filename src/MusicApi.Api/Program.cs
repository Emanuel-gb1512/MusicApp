// Punto de entrada de MusicApi (backend de TONYX).
// En tareas posteriores añadiremos aquí: autenticación JWT, CORS,
// rate limiting, manejo centralizado de excepciones, etc.

using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.OpenApi;
using MusicApi.Application.Auth;
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

    // Habilita el uso de controladores. Los enumerados se escriben con su nombre
    // ("Android", "Windows"...) y no distinguen mayúsculas.
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    // Swagger / OpenAPI (Swashbuckle).
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "TONYX · MusicApi",
            Version = "v1",
            Description = "API REST del backend de TONYX."
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