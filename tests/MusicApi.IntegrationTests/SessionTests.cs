using System.Net;
using MusicApi.Application.Auth;
using MusicApi.Domain.Enums;
using MusicApi.IntegrationTests.Infrastructure;
using Xunit;

namespace MusicApi.IntegrationTests;

[Collection(ApiCollection.Name)]
public class SessionTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        Client.SendAsync(HttpMethod.Post, "/api/auth/refresh", body: new RefreshRequest(refreshToken));

    private Task<HttpResponseMessage> MeAsync(string accessToken) =>
        Client.SendAsync(HttpMethod.Get, "/api/auth/me", accessToken);

    [Fact]
    public async Task Refresh_rotates_the_tokens()
    {
        var auth = await Client.RegisterOkAsync(ApiClient.NewEmail());

        var response = await RefreshAsync(auth.RefreshToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var renewed = await ApiClient.ReadAsync<AuthResponse>(response);
        Assert.NotEqual(auth.RefreshToken, renewed.RefreshToken);
        Assert.NotEqual(auth.AccessToken, renewed.AccessToken);
        Assert.Equal(auth.SessionId, renewed.SessionId);

        var me = await MeAsync(renewed.AccessToken);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Reusing_an_old_refresh_token_closes_the_whole_session()
    {
        var auth = await Client.RegisterOkAsync(ApiClient.NewEmail());
        var first = await RefreshAsync(auth.RefreshToken);
        var renewed = await ApiClient.ReadAsync<AuthResponse>(first);

        // Alguien presenta el token viejo, que ya se gastó: se asume que fue robado.
        var reuse = await RefreshAsync(auth.RefreshToken);
        await ApiClient.AssertErrorAsync(reuse, HttpStatusCode.Unauthorized, "invalid_refresh_token");

        // La sesión entera quedó cerrada: ni el token nuevo ni su access token sirven ya.
        var newer = await RefreshAsync(renewed.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, newer.StatusCode);

        var me = await MeAsync(renewed.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Refresh_with_an_unknown_token_is_rejected()
    {
        var response = await RefreshAsync(new string('A', 43));

        await ApiClient.AssertErrorAsync(response, HttpStatusCode.Unauthorized, "invalid_refresh_token");
    }

    [Fact]
    public async Task Logout_closes_the_current_session_immediately()
    {
        var auth = await Client.RegisterOkAsync(ApiClient.NewEmail());

        var logout = await Client.SendAsync(HttpMethod.Post, "/api/auth/logout", auth.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        // El access token aún no caducó, pero la sesión ya no está activa.
        var me = await MeAsync(auth.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);

        var refresh = await RefreshAsync(auth.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Logout_others_keeps_only_the_current_session()
    {
        var email = ApiClient.NewEmail();
        var a = await Client.RegisterOkAsync(email, ApiClient.Device("dev-a"));
        var b = await Client.LoginOkAsync(email, device: ApiClient.Device("dev-b", DevicePlatform.Android));

        var list = await Client.SendAsync(HttpMethod.Get, "/api/auth/sessions", a.AccessToken);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var sessions = await ApiClient.ReadAsync<List<SessionInfo>>(list);
        Assert.Equal(2, sessions.Count);
        var current = Assert.Single(sessions, s => s.IsCurrent);
        Assert.Equal(a.SessionId, current.SessionId);

        var others = await Client.SendAsync(HttpMethod.Post, "/api/auth/logout-others", a.AccessToken);
        Assert.Equal(HttpStatusCode.OK, others.StatusCode);
        var result = await ApiClient.ReadAsync<RevokeOthersResponse>(others);
        Assert.Equal(1, result.RevokedSessions);

        Assert.Equal(HttpStatusCode.Unauthorized, (await MeAsync(b.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await MeAsync(a.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task A_session_can_be_revoked_but_only_by_its_owner()
    {
        var email = ApiClient.NewEmail();
        var a = await Client.RegisterOkAsync(email, ApiClient.Device("dev-a"));
        var b = await Client.LoginOkAsync(email, device: ApiClient.Device("dev-b", DevicePlatform.Android));

        var revoke = await Client.SendAsync(HttpMethod.Delete, $"/api/auth/sessions/{b.SessionId}", a.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        var again = await Client.SendAsync(HttpMethod.Delete, $"/api/auth/sessions/{b.SessionId}", a.AccessToken);
        await ApiClient.AssertErrorAsync(again, HttpStatusCode.NotFound, "session_not_found");

        // Otro usuario no puede cerrar las sesiones de este.
        var stranger = await Client.RegisterOkAsync(ApiClient.NewEmail());
        var foreign = await Client.SendAsync(HttpMethod.Delete, $"/api/auth/sessions/{a.SessionId}", stranger.AccessToken);
        await ApiClient.AssertErrorAsync(foreign, HttpStatusCode.NotFound, "session_not_found");

        Assert.Equal(HttpStatusCode.OK, (await MeAsync(a.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task Protected_endpoints_reject_a_missing_or_invalid_token()
    {
        var withoutToken = await Client.SendAsync(HttpMethod.Get, "/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, withoutToken.StatusCode);

        var garbage = await MeAsync("esto-no-es-un-jwt");
        Assert.Equal(HttpStatusCode.Unauthorized, garbage.StatusCode);
    }
}