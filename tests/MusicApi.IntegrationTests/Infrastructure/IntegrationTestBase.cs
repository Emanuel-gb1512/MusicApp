using System.Net;
using MusicApi.Application.Auth;
using Xunit;

namespace MusicApi.IntegrationTests.Infrastructure;

/// <summary>Base de las pruebas: un cliente HTTP, acceso a los correos capturados y utilidades comunes.</summary>
public abstract class IntegrationTestBase(ApiFactory factory)
{
    protected ApiFactory Factory { get; } = factory;

    protected ApiClient Client { get; } = new(factory.CreateApiClient());

    protected CapturingEmailSender Emails => Factory.Emails;

    /// <summary>Recorre el flujo real de verificación: pide el código, lo lee del correo y lo usa.</summary>
    protected async Task VerifyEmailAsync(string email, AuthResponse auth)
    {
        var seen = Emails.CountFor(email);

        var send = await Client.SendAsync(HttpMethod.Post, "/api/auth/verify-email/send", auth.AccessToken);
        Assert.Equal(HttpStatusCode.Accepted, send.StatusCode);

        var message = await Emails.WaitForNextAsync(email, seen);

        var verify = await Client.SendAsync(
            HttpMethod.Post,
            "/api/auth/verify-email",
            body: new VerifyEmailRequest(CapturingEmailSender.ExtractCode(message)));

        Assert.Equal(HttpStatusCode.NoContent, verify.StatusCode);
    }
}