using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MusicApi.Api.Models;
using MusicApi.Application.Auth;
using MusicApi.Domain.Enums;
using Xunit;

namespace MusicApi.IntegrationTests.Infrastructure;

/// <summary>Opciones de JSON iguales a las de la API (camelCase y enumerados como texto).</summary>
public static class Json
{
    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

/// <summary>Cliente de pruebas: envuelve las llamadas HTTP más habituales.</summary>
public sealed class ApiClient(HttpClient http)
{
    public const string DefaultPassword = "ClaveSegura2026";

    /// <summary>Correo único por prueba, para que no se estorben entre sí.</summary>
    public static string NewEmail() => $"it-{Guid.NewGuid():N}@tonyx.test";

    public static DeviceInfo Device(string identifier = "pc-test", DevicePlatform platform = DevicePlatform.Windows) =>
        new(identifier, $"Dispositivo {identifier}", platform, "0.1.0");

    public Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string url,
        string? accessToken = null,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, url);

        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: Json.Options);
        }

        return http.SendAsync(request);
    }

    public Task<HttpResponseMessage> RegisterAsync(
        string email,
        string password = DefaultPassword,
        DeviceInfo? device = null) =>
        SendAsync(
            HttpMethod.Post,
            "/api/auth/register",
            body: new RegisterRequest(email, password, "Usuario de prueba", device ?? Device()));

    public Task<HttpResponseMessage> LoginAsync(
        string email,
        string password = DefaultPassword,
        DeviceInfo? device = null) =>
        SendAsync(
            HttpMethod.Post,
            "/api/auth/login",
            body: new LoginRequest(email, password, device ?? Device()));

    /// <summary>Registra una cuenta y comprueba que salió bien (201).</summary>
    public async Task<AuthResponse> RegisterOkAsync(string email, DeviceInfo? device = null)
    {
        var response = await RegisterAsync(email, device: device);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<AuthResponse>(response);
    }

    /// <summary>Inicia sesión y comprueba que salió bien (200).</summary>
    public async Task<AuthResponse> LoginOkAsync(
        string email,
        string password = DefaultPassword,
        DeviceInfo? device = null)
    {
        var response = await LoginAsync(email, password, device);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync<AuthResponse>(response);
    }

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(Json.Options);
        return value ?? throw new InvalidOperationException("La respuesta no contiene datos.");
    }

    /// <summary>Comprueba el código HTTP y el código de error estable de la respuesta.</summary>
    public static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var error = await ReadAsync<ApiError>(response);
        Assert.Equal(code, error.Code);
    }
}