using MusicApi.Application.Music;
using Xunit;

namespace MusicApi.UnitTests;

public class MusicSearchValidatorTests
{
    private readonly MusicSearchRequestValidator _validator = new();

    [Fact]
    public void A_valid_request_passes()
    {
        Assert.True(_validator.Validate(new MusicSearchRequest("rock", "jamendo", 20)).IsValid);
    }

    [Fact]
    public void An_empty_query_is_rejected()
    {
        Assert.False(_validator.Validate(new MusicSearchRequest("   ", null, 20)).IsValid);
    }

    [Fact]
    public void An_oversized_query_is_rejected()
    {
        Assert.False(_validator.Validate(new MusicSearchRequest(new string('a', 101), null, 20)).IsValid);
    }

    [Fact]
    public void A_limit_below_one_is_rejected()
    {
        Assert.False(_validator.Validate(new MusicSearchRequest("rock", null, 0)).IsValid);
    }

    [Fact]
    public void A_limit_above_fifty_is_rejected()
    {
        Assert.False(_validator.Validate(new MusicSearchRequest("rock", null, 51)).IsValid);
    }
}