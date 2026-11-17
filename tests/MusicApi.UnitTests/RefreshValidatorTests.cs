using MusicApi.Application.Auth;
using Xunit;

namespace MusicApi.UnitTests;

public class RefreshValidatorTests
{
    private readonly RefreshRequestValidator _validator = new();

    [Fact]
    public void Valid_refresh_request_passes()
    {
        Assert.True(_validator.Validate(new RefreshRequest(new string('a', 43))).IsValid);
    }

    [Fact]
    public void Empty_refresh_token_is_rejected()
    {
        Assert.False(_validator.Validate(new RefreshRequest("")).IsValid);
    }

    [Fact]
    public void Oversized_refresh_token_is_rejected()
    {
        Assert.False(_validator.Validate(new RefreshRequest(new string('a', 201))).IsValid);
    }
}