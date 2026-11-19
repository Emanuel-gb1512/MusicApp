using System.Net;
using MusicApi.Application.Auth;
using MusicApi.Domain.Enums;
using MusicApi.IntegrationTests.Infrastructure;
using Xunit;

namespace MusicApi.IntegrationTests;

[Collection(ApiCollection.Name)]
public class GoogleFlowTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private Task<HttpResponseMessage> GoogleAsync(string idToken) =>
        Client.SendAsync(
            HttpMethod.Post,
            "/api/auth/google",
            body: new GoogleLoginRequest(idToken, ApiClient.Device("movil-g", DevicePlatform.Android)));

    private static string NewSubject() => $"sub-{Guid.NewGuid():N}";

    [Fact]
    public async Task First_google_login_creates_the_account_and_the_next_one_reuses_it()
    {
        var email = ApiClient.NewEmail();
        var token = FakeGoogleTokenValidator.Token(NewSubject(), email);

        var first = await GoogleAsync(token);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var created = await ApiClient.ReadAsync<AuthResponse>(first);
        Assert.True(created.User.EmailConfirmed);
        Assert.Equal(email, created.User.Email);

        var second = await GoogleAsync(token);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var again = await ApiClient.ReadAsync<AuthResponse>(second);
        Assert.Equal(created.User.Id, again.User.Id);

        var me = await Client.SendAsync(HttpMethod.Get, "/api/auth/me", again.AccessToken);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task An_email_not_verified_by_google_is_rejected()
    {
        var token = FakeGoogleTokenValidator.Token(NewSubject(), ApiClient.NewEmail(), verified: false);

        var response = await GoogleAsync(token);

        await ApiClient.AssertErrorAsync(response, HttpStatusCode.Forbidden, "email_not_verified_by_provider");
    }

    [Fact]
    public async Task An_invalid_google_token_is_rejected()
    {
        var response = await GoogleAsync("esto-no-es-un-token-de-google");

        await ApiClient.AssertErrorAsync(response, HttpStatusCode.Unauthorized, "invalid_external_token");
    }

    [Fact]
    public async Task An_account_created_with_google_has_no_password()
    {
        var email = ApiClient.NewEmail();
        await GoogleAsync(FakeGoogleTokenValidator.Token(NewSubject(), email));

        var login = await Client.LoginAsync(email, ApiClient.DefaultPassword);

        await ApiClient.AssertErrorAsync(login, HttpStatusCode.Unauthorized, "invalid_credentials");
    }

    [Fact]
    public async Task Google_claims_an_unverified_account_and_locks_out_whoever_created_it()
    {
        // Un suplantador se registra con el correo de otra persona, sin poder verificarlo.
        var email = ApiClient.NewEmail();
        var impostor = await Client.RegisterOkAsync(email);

        // El dueño real entra con Google, que garantiza ese correo.
        var response = await GoogleAsync(FakeGoogleTokenValidator.Token(NewSubject(), email));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var owner = await ApiClient.ReadAsync<AuthResponse>(response);
        Assert.True(owner.User.EmailConfirmed);

        // El suplantador pierde la sesión y la contraseña.
        var impostorSession = await Client.SendAsync(HttpMethod.Get, "/api/auth/me", impostor.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, impostorSession.StatusCode);

        var impostorLogin = await Client.LoginAsync(email, ApiClient.DefaultPassword);
        await ApiClient.AssertErrorAsync(impostorLogin, HttpStatusCode.Unauthorized, "invalid_credentials");

        var ownerSession = await Client.SendAsync(HttpMethod.Get, "/api/auth/me", owner.AccessToken);
        Assert.Equal(HttpStatusCode.OK, ownerSession.StatusCode);
    }

    [Fact]
    public async Task Google_links_to_a_verified_account_without_touching_its_password()
    {
        var email = ApiClient.NewEmail();
        var auth = await Client.RegisterOkAsync(email);
        await VerifyEmailAsync(email, auth);

        var google = await GoogleAsync(FakeGoogleTokenValidator.Token(NewSubject(), email));
        Assert.Equal(HttpStatusCode.OK, google.StatusCode);

        // La contraseña sigue funcionando: la cuenta ya era de su dueño verificado.
        var login = await Client.LoginAsync(email, ApiClient.DefaultPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }
}