using Microsoft.Extensions.Logging.Abstractions;
using MusicApi.Application.Auth;
using MusicApi.Application.Settings;
using MusicApi.Domain.Enums;
using MusicApi.Infrastructure.Auth;
using Xunit;

namespace MusicApi.UnitTests;

public class GoogleLoginValidatorTests
{
    private static readonly DeviceInfo ValidDevice =
        new("pc-001", "PC de pruebas", DevicePlatform.Windows, "0.1.0");

    private readonly GoogleLoginRequestValidator _validator = new();

    [Fact]
    public void Valid_request_passes()
    {
        Assert.True(_validator.Validate(new GoogleLoginRequest("eyJ.token.firma", ValidDevice)).IsValid);
    }

    [Fact]
    public void Empty_token_is_rejected()
    {
        Assert.False(_validator.Validate(new GoogleLoginRequest("", ValidDevice)).IsValid);
    }

    [Fact]
    public void Oversized_token_is_rejected()
    {
        Assert.False(_validator.Validate(new GoogleLoginRequest(new string('a', 4097), ValidDevice)).IsValid);
    }

    [Fact]
    public void Missing_device_is_rejected()
    {
        Assert.False(_validator.Validate(new GoogleLoginRequest("eyJ.token.firma", null!)).IsValid);
    }
}

public class GoogleTokenValidatorTests
{
    private static GoogleTokenValidator Create(params string[] clientIds) =>
        new(new GoogleOptions { ClientIds = clientIds }, NullLogger<GoogleTokenValidator>.Instance);

    [Fact]
    public void Is_not_configured_without_client_ids()
    {
        Assert.False(Create().IsConfigured);
    }

    [Fact]
    public void Blank_client_ids_do_not_count_as_configured()
    {
        Assert.False(Create("", "   ").IsConfigured);
    }

    [Fact]
    public void Is_configured_with_a_client_id()
    {
        Assert.True(Create("123.apps.googleusercontent.com").IsConfigured);
    }

    [Fact]
    public async Task Validate_returns_null_when_not_configured()
    {
        Assert.Null(await Create().ValidateAsync("cualquier-cosa"));
    }
}