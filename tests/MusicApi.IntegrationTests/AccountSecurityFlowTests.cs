using System.Net;
using MusicApi.Application.Auth;
using MusicApi.IntegrationTests.Infrastructure;
using Xunit;

namespace MusicApi.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AccountSecurityFlowTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private const string NewPassword = "NuevaClave2026";

    private Task<HttpResponseMessage> ForgotAsync(string email) =>
        Client.SendAsync(HttpMethod.Post, "/api/auth/forgot-password", body: new ForgotPasswordRequest(email));

    private Task<HttpResponseMessage> ResetAsync(string code, string newPassword) =>
        Client.SendAsync(HttpMethod.Post, "/api/auth/reset-password", body: new ResetPasswordRequest(code, newPassword));

    [Fact]
    public async Task Email_verification_works_once_and_respects_the_cooldown()
    {
        var email = ApiClient.NewEmail();
        var auth = await Client.RegisterOkAsync(email);
        var seen = Emails.CountFor(email);

        var send = await Client.SendAsync(HttpMethod.Post, "/api/auth/verify-email/send", auth.AccessToken);
        Assert.Equal(HttpStatusCode.Accepted, send.StatusCode);
        var code = CapturingEmailSender.ExtractCode(await Emails.WaitForNextAsync(email, seen));

        // Pedir otro código enseguida está limitado a uno por minuto.
        var tooSoon = await Client.SendAsync(HttpMethod.Post, "/api/auth/verify-email/send", auth.AccessToken);
        await ApiClient.AssertErrorAsync(tooSoon, HttpStatusCode.TooManyRequests, "too_many_requests");

        var verify = await Client.SendAsync(
            HttpMethod.Post, "/api/auth/verify-email", body: new VerifyEmailRequest(code));
        Assert.Equal(HttpStatusCode.NoContent, verify.StatusCode);

        var me = await Client.SendAsync(HttpMethod.Get, "/api/auth/me", auth.AccessToken);
        var user = await ApiClient.ReadAsync<UserSummary>(me);
        Assert.True(user.EmailConfirmed);

        // El código es de un solo uso.
        var reuse = await Client.SendAsync(
            HttpMethod.Post, "/api/auth/verify-email", body: new VerifyEmailRequest(code));
        await ApiClient.AssertErrorAsync(reuse, HttpStatusCode.BadRequest, "invalid_token");

        var alreadyVerified = await Client.SendAsync(
            HttpMethod.Post, "/api/auth/verify-email/send", auth.AccessToken);
        await ApiClient.AssertErrorAsync(alreadyVerified, HttpStatusCode.Conflict, "email_already_verified");
    }

    [Fact]
    public async Task Forgot_password_answers_the_same_whether_or_not_the_account_exists()
    {
        var unknown = ApiClient.NewEmail();
        var known = ApiClient.NewEmail();
        await Client.RegisterOkAsync(known);
        var seen = Emails.CountFor(known);

        Assert.Equal(HttpStatusCode.Accepted, (await ForgotAsync(unknown)).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await ForgotAsync(known)).StatusCode);

        // Solo la cuenta real recibe correo. Se espera al suyo para estar seguros de que la cola ya se vació.
        await Emails.WaitForNextAsync(known, seen);
        Assert.Equal(0, Emails.CountFor(unknown));
    }

    [Fact]
    public async Task Resetting_the_password_changes_it_and_closes_every_session()
    {
        var email = ApiClient.NewEmail();
        var auth = await Client.RegisterOkAsync(email);
        var seen = Emails.CountFor(email);

        await ForgotAsync(email);
        var code = CapturingEmailSender.ExtractCode(await Emails.WaitForNextAsync(email, seen));

        // Una contraseña débil se rechaza SIN gastar el código.
        var weak = await ResetAsync(code, "corta");
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);

        var reset = await ResetAsync(code, NewPassword);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        // La contraseña vieja ya no sirve; la nueva sí, y el correo quedó verificado.
        var oldLogin = await Client.LoginAsync(email, ApiClient.DefaultPassword);
        await ApiClient.AssertErrorAsync(oldLogin, HttpStatusCode.Unauthorized, "invalid_credentials");

        var newLogin = await Client.LoginOkAsync(email, NewPassword);
        Assert.True(newLogin.User.EmailConfirmed);

        // La sesión anterior al cambio quedó cerrada.
        var oldSession = await Client.SendAsync(HttpMethod.Get, "/api/auth/me", auth.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, oldSession.StatusCode);

        // El código es de un solo uso.
        var reuse = await ResetAsync(code, NewPassword);
        await ApiClient.AssertErrorAsync(reuse, HttpStatusCode.BadRequest, "invalid_token");
    }

    [Fact]
    public async Task Reset_with_an_unknown_code_is_rejected()
    {
        var response = await ResetAsync(new string('A', 43), NewPassword);

        await ApiClient.AssertErrorAsync(response, HttpStatusCode.BadRequest, "invalid_token");
    }
}