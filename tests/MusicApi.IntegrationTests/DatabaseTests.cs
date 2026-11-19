using System.Net;
using Microsoft.Data.SqlClient;
using MusicApi.Application.Interfaces;
using MusicApi.IntegrationTests.Infrastructure;
using Xunit;

namespace MusicApi.IntegrationTests;

[Collection(ApiCollection.Name)]
public class DatabaseTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task The_scripts_and_the_entity_mappings_agree()
    {
        var response = await Client.SendAsync(HttpMethod.Get, "/api/health/database");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var health = await ApiClient.ReadAsync<DatabaseHealthResult>(response);
        Assert.True(health.Connected);
        Assert.Equal(TestDatabase.DatabaseName, health.DatabaseName);
        Assert.Equal(6, health.LatestScriptNumber);
        Assert.Equal(7, health.ProviderCount);
        Assert.Equal(15, health.GenreCount);
        Assert.Equal(8, health.IdentityTablesChecked);
    }

    [Fact]
    public async Task Passwords_and_refresh_tokens_are_stored_only_as_hashes()
    {
        var email = ApiClient.NewEmail();
        var auth = await Client.RegisterOkAsync(email);

        await using var connection = new SqlConnection(TestDatabase.ConnectionString);
        await connection.OpenAsync();

        await using var passwordQuery = new SqlCommand(
            "SELECT PasswordHash FROM dbo.Users WHERE Email = @email", connection);
        passwordQuery.Parameters.AddWithValue("@email", email);
        var passwordHash = (string?)await passwordQuery.ExecuteScalarAsync();

        Assert.False(string.IsNullOrEmpty(passwordHash));
        Assert.DoesNotContain(ApiClient.DefaultPassword, passwordHash);

        await using var tokenQuery = new SqlCommand(
            "SELECT TokenHash FROM dbo.RefreshTokens WHERE SessionId = @sessionId", connection);
        tokenQuery.Parameters.AddWithValue("@sessionId", auth.SessionId);
        var tokenHash = (string?)await tokenQuery.ExecuteScalarAsync();

        Assert.NotNull(tokenHash);
        Assert.Equal(64, tokenHash.Length);
        Assert.NotEqual(auth.RefreshToken, tokenHash);
    }
}