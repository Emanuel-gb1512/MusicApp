using MusicApi.Application.Auth;
using MusicApi.Application.Settings;
using MusicApi.Infrastructure.Messaging;
using Xunit;

namespace MusicApi.UnitTests;

public class ResetPasswordValidatorTests
{
    private readonly ResetPasswordRequestValidator _validator = new();

    [Fact]
    public void Valid_request_passes()
    {
        Assert.True(_validator.Validate(new ResetPasswordRequest("abc123", "NuevaClave2026")).IsValid);
    }

    [Fact]
    public void Short_password_is_rejected()
    {
        Assert.False(_validator.Validate(new ResetPasswordRequest("abc123", "Corta1")).IsValid);
    }

    [Fact]
    public void Password_without_digit_is_rejected()
    {
        Assert.False(_validator.Validate(new ResetPasswordRequest("abc123", "SoloLetrasAquiHoy")).IsValid);
    }

    [Fact]
    public void Empty_token_is_rejected()
    {
        Assert.False(_validator.Validate(new ResetPasswordRequest("", "NuevaClave2026")).IsValid);
    }
}

public class EmailOptionsTests
{
    private static EmailOptions Smtp() => new()
    {
        Provider = "Smtp",
        SmtpHost = "smtp.example.com",
        SmtpPort = 587,
        SmtpUsername = "usuario",
        SmtpPassword = "secreto",
        FromAddress = "no-reply@example.com"
    };

    [Fact]
    public void Disabled_is_valid_everywhere()
    {
        var options = new EmailOptions { Provider = "Disabled" };

        Assert.Null(Record.Exception(() => options.Validate(isDevelopment: false)));
    }

    [Fact]
    public void Log_is_valid_in_development()
    {
        var options = new EmailOptions { Provider = "Log" };

        Assert.Null(Record.Exception(() => options.Validate(isDevelopment: true)));
    }

    [Fact]
    public void Log_is_rejected_outside_development()
    {
        var options = new EmailOptions { Provider = "Log" };

        Assert.Throws<InvalidOperationException>(() => options.Validate(isDevelopment: false));
    }

    [Fact]
    public void Unknown_provider_is_rejected()
    {
        var options = new EmailOptions { Provider = "Paloma" };

        Assert.Throws<InvalidOperationException>(() => options.Validate(isDevelopment: true));
    }

    [Fact]
    public void Smtp_without_host_is_rejected()
    {
        var options = Smtp();
        options.SmtpHost = "";

        Assert.Throws<InvalidOperationException>(() => options.Validate(isDevelopment: false));
    }

    [Fact]
    public void Complete_smtp_configuration_is_valid()
    {
        Assert.Null(Record.Exception(() => Smtp().Validate(isDevelopment: false)));
    }
}

public class EmailTemplatesTests
{
    [Fact]
    public void Verification_email_contains_the_code_in_both_bodies()
    {
        var message = EmailTemplates.VerifyEmail("ana@example.com", "Ana", "CODIGO-DE-PRUEBA");

        Assert.Equal("ana@example.com", message.To);
        Assert.Contains("CODIGO-DE-PRUEBA", message.TextBody);
        Assert.Contains("CODIGO-DE-PRUEBA", message.HtmlBody);
    }

    [Fact]
    public void Display_name_is_html_encoded_in_the_html_body()
    {
        var message = EmailTemplates.PasswordReset("ana@example.com", "<b>Ana</b>", "CODIGO");

        Assert.DoesNotContain("<b>Ana</b>", message.HtmlBody);
        Assert.Contains("&lt;b&gt;", message.HtmlBody);
    }
}