using MusicApi.Application.Auth;
using MusicApi.Domain.Enums;
using Xunit;

namespace MusicApi.UnitTests;

public class AuthValidatorTests
{
    private static readonly DeviceInfo ValidDevice =
        new("pc-001", "PC de pruebas", DevicePlatform.Windows, "0.1.0");

    private static RegisterRequest ValidRegister() =>
        new("ana@example.com", "ClaveSegura2026", "Ana", ValidDevice);

    private readonly RegisterRequestValidator _register = new();
    private readonly LoginRequestValidator _login = new();

    [Fact]
    public void Valid_register_request_passes()
    {
        Assert.True(_register.Validate(ValidRegister()).IsValid);
    }

    [Fact]
    public void Register_rejects_invalid_email()
    {
        var result = _register.Validate(ValidRegister() with { Email = "no-es-un-correo" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Email");
    }

    [Fact]
    public void Register_rejects_email_without_dot_in_domain()
    {
        var result = _register.Validate(ValidRegister() with { Email = "ana@localhost" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Email");
    }

    [Fact]
    public void Register_rejects_short_password()
    {
        var result = _register.Validate(ValidRegister() with { Password = "Corta1" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Password");
    }

    [Fact]
    public void Register_rejects_password_without_digit()
    {
        var result = _register.Validate(ValidRegister() with { Password = "SoloLetrasAquiHoy" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Password");
    }

    [Fact]
    public void Register_rejects_password_without_letter()
    {
        var result = _register.Validate(ValidRegister() with { Password = "12345678901" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Password");
    }

    [Fact]
    public void Register_rejects_password_equal_to_email()
    {
        var request = ValidRegister() with
        {
            Email = "usuario12@example.com",
            Password = "usuario12@example.com"
        };

        var result = _register.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Password");
    }

    [Fact]
    public void Register_rejects_blank_display_name()
    {
        var result = _register.Validate(ValidRegister() with { DisplayName = "   " });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "DisplayName");
    }

    [Fact]
    public void Register_rejects_missing_device()
    {
        var result = _register.Validate(ValidRegister() with { Device = null! });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Device");
    }

    [Fact]
    public void Register_rejects_unknown_platform()
    {
        var request = ValidRegister() with
        {
            Device = ValidDevice with { Platform = (DevicePlatform)99 }
        };

        var result = _register.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Device.Platform");
    }

    [Fact]
    public void Valid_login_request_passes()
    {
        var result = _login.Validate(new LoginRequest("ana@example.com", "lo-que-sea", ValidDevice));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Login_rejects_empty_password()
    {
        var result = _login.Validate(new LoginRequest("ana@example.com", "", ValidDevice));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Password");
    }
}