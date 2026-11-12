using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MusicApi.Application.Interfaces;
using MusicApi.Application.Settings;
using MusicApi.Infrastructure.Security;
using Xunit;

namespace MusicApi.UnitTests;

public class PasswordServiceTests
{
    private readonly PasswordService _service = new();

    [Fact]
    public void Hash_does_not_contain_the_plain_password()
    {
        var hash = _service.Hash("MiClaveSegura#2026");

        Assert.False(string.IsNullOrWhiteSpace(hash));
        Assert.DoesNotContain("MiClaveSegura", hash);
    }

    [Fact]
    public void Verify_with_correct_password_succeeds()
    {
        var hash = _service.Hash("MiClaveSegura#2026");

        Assert.Equal(PasswordVerificationOutcome.Success, _service.Verify(hash, "MiClaveSegura#2026"));
    }

    [Fact]
    public void Verify_with_wrong_password_fails()
    {
        var hash = _service.Hash("MiClaveSegura#2026");

        Assert.Equal(PasswordVerificationOutcome.Failed, _service.Verify(hash, "otra-clave"));
    }

    [Fact]
    public void Same_password_produces_different_hashes()
    {
        var first = _service.Hash("MiClaveSegura#2026");
        var second = _service.Hash("MiClaveSegura#2026");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Verify_with_garbage_hash_fails()
    {
        Assert.Equal(PasswordVerificationOutcome.Failed, _service.Verify("esto-no-es-un-hash", "lo-que-sea"));
    }
}

public class SecureTokenServiceTests
{
    private readonly SecureTokenService _service = new();

    [Fact]
    public void Generate_returns_url_safe_token_and_matching_hash()
    {
        var generated = _service.Generate();

        Assert.Equal(43, generated.Token.Length);
        Assert.Matches("^[A-Za-z0-9_-]+$", generated.Token);
        Assert.Equal(64, generated.TokenHash.Length);
        Assert.Matches("^[0-9a-f]{64}$", generated.TokenHash);
        Assert.Equal(_service.Hash(generated.Token), generated.TokenHash);
    }

    [Fact]
    public void Two_generated_tokens_are_different()
    {
        var first = _service.Generate();
        var second = _service.Generate();

        Assert.NotEqual(first.Token, second.Token);
        Assert.NotEqual(first.TokenHash, second.TokenHash);
    }

    [Fact]
    public void Hash_is_deterministic()
    {
        Assert.Equal(_service.Hash("abc"), _service.Hash("abc"));
    }
}

public class JwtTokenServiceTests
{
    private static JwtOptions CreateOptions(string? signingKey = null) => new()
    {
        Issuer = "TONYX-test",
        Audience = "tonyx-test-app",
        SigningKey = signingKey ?? new string('k', 64),
        AccessTokenMinutes = 15,
        RefreshTokenDays = 30
    };

    private static TokenValidationParameters CreateValidation(JwtOptions options) => new()
    {
        ValidIssuer = options.Issuer,
        ValidAudience = options.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(options.GetSigningKeyBytes()),
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    [Fact]
    public void CreateAccessToken_contains_the_expected_claims()
    {
        var options = CreateOptions();
        var service = new JwtTokenService(options);
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var result = service.CreateAccessToken(userId, sessionId, "ana@example.com");
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(result.Token);

        Assert.Equal("TONYX-test", jwt.Issuer);
        Assert.Contains("tonyx-test-app", jwt.Audiences);
        Assert.Equal(userId.ToString(), jwt.Claims.First(c => c.Type == "sub").Value);
        Assert.Equal(sessionId.ToString(), jwt.Claims.First(c => c.Type == "sid").Value);
        Assert.Equal("ana@example.com", jwt.Claims.First(c => c.Type == "email").Value);
        Assert.False(string.IsNullOrWhiteSpace(jwt.Claims.First(c => c.Type == "jti").Value));
        Assert.InRange(result.ExpiresAtUtc, DateTime.UtcNow.AddMinutes(14), DateTime.UtcNow.AddMinutes(16));
    }

    [Fact]
    public async Task A_token_signed_with_the_right_key_is_valid()
    {
        var options = CreateOptions();
        var service = new JwtTokenService(options);
        var result = service.CreateAccessToken(Guid.NewGuid(), Guid.NewGuid(), "ana@example.com");

        var validation = await new JsonWebTokenHandler()
            .ValidateTokenAsync(result.Token, CreateValidation(options));

        Assert.True(validation.IsValid);
    }

    [Fact]
    public async Task A_token_is_rejected_when_checked_with_a_different_key()
    {
        var service = new JwtTokenService(CreateOptions());
        var result = service.CreateAccessToken(Guid.NewGuid(), Guid.NewGuid(), "ana@example.com");
        var otherKey = CreateOptions(new string('x', 64));

        var validation = await new JsonWebTokenHandler()
            .ValidateTokenAsync(result.Token, CreateValidation(otherKey));

        Assert.False(validation.IsValid);
    }

    [Fact]
    public void Options_reject_a_signing_key_that_is_too_short()
    {
        Assert.Throws<InvalidOperationException>(() => CreateOptions("corta").Validate());
    }

    [Fact]
    public void Options_accept_a_valid_configuration()
    {
        var exception = Record.Exception(() => CreateOptions().Validate());

        Assert.Null(exception);
    }
}
