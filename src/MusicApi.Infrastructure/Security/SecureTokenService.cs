using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using MusicApi.Application.Interfaces;

namespace MusicApi.Infrastructure.Security;

/// <summary>
/// Tokens aleatorios de 32 bytes (generador criptográfico) y su hash SHA-256.
/// </summary>
public sealed class SecureTokenService : ISecureTokenService
{
    private const int TokenBytes = 32;

    public GeneratedToken Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenBytes);
        var token = Base64Url.EncodeToString(bytes);
        return new GeneratedToken(token, Hash(token));
    }

    public string Hash(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
