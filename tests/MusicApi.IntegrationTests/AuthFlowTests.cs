using System.Net;
using MusicApi.Application.Auth;
using MusicApi.IntegrationTests.Infrastructure;
using Xunit;

namespace MusicApi.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AuthFlowTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Register_creates_the_account_and_returns_working_tokens()
    {
        var email = ApiClient.NewEmail();

        var response = await Client.RegisterAsync(email);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var auth = await ApiClient.ReadAsync<AuthResponse>(response);
        Assert.False(string.IsNullOrWhiteSpace(auth.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(auth.RefreshToken));
        Assert.False(auth.User.EmailConfirmed);

        var me = await Client.SendAsync(HttpMethod.Get, "/api/auth/me", auth.AccessToken);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var user = await ApiClient.ReadAsync<UserSummary>(me);
        Assert.Equal(email, user.Email);
    }

    [Fact]
    public async Task Register_with_an_existing_email_is_a_conflict_even_if_the_case_differs()
    {
        var email = ApiClient.NewEmail();
        await Client.RegisterOkAsync(email);

        var duplicate = await Client.RegisterAsync(email.ToUpperInvariant());

        await ApiClient.AssertErrorAsync(duplicate, HttpStatusCode.Conflict, "email_already_registered");
    }

    [Fact]
    public async Task Register_with_a_weak_password_is_rejected()
    {
        var response = await Client.RegisterAsync(ApiClient.NewEmail(), password: "corta");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_the_same_device_reuses_it()
    {
        var email = ApiClient.NewEmail();
        var device = ApiClient.Device("dev-1");
        var registered = await Client.RegisterOkAsync(email, device);

        var login = await Client.LoginOkAsync(email, device: device);

        Assert.Equal(registered.DeviceId, login.DeviceId);
        Assert.NotEqual(registered.SessionId, login.SessionId);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_get_the_same_answer()
    {
        var email = ApiClient.NewEmail();
        await Client.RegisterOkAsync(email);

        var wrongPassword = await Client.LoginAsync(email, "OtraClave99999");
        var unknownEmail = await Client.LoginAsync(ApiClient.NewEmail());

        await ApiClient.AssertErrorAsync(wrongPassword, HttpStatusCode.Unauthorized, "invalid_credentials");
        await ApiClient.AssertErrorAsync(unknownEmail, HttpStatusCode.Unauthorized, "invalid_credentials");
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account()
    {
        var email = ApiClient.NewEmail();
        await Client.RegisterOkAsync(email);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failed = await Client.LoginAsync(email, "OtraClave99999");
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        // Aunque ahora la contraseña sea la correcta, la cuenta está bloqueada.
        var locked = await Client.LoginAsync(email);

        await ApiClient.AssertErrorAsync(locked, HttpStatusCode.TooManyRequests, "account_locked");
    }
}