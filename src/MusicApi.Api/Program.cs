// Punto de entrada de MusicApi (backend de TONYX).
// En tareas posteriores iremos añadiendo aquí: Swagger, base de datos,
// autenticación, CORS, rate limiting, logs, etc.

var builder = WebApplication.CreateBuilder(args);

// Habilita el uso de controladores (clases en la carpeta Controllers).
builder.Services.AddControllers();

// Genera el documento OpenAPI (la descripción de la API en JSON).
// En una tarea posterior lo conectaremos con la interfaz Swagger.
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// Endpoint de salud: sirve para comprobar que la API está viva.
app.MapGet("/api/health", () => Results.Ok(new
{
    app = "TONYX",
    service = "MusicApi",
    status = "ok",
    utc = DateTime.UtcNow
}));

app.Run();