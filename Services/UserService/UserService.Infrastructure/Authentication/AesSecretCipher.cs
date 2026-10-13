using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Application.Features.Identity;

namespace UserService.Infrastructure.Authentication;

public sealed class AesSecretCipher(IConfiguration configuration) : ISecretCipher
{
    private byte[] Key()
    {
        var encoded = configuration["Security:EncryptionKey"];
        if (string.IsNullOrWhiteSpace(encoded)) throw new DependencyUnavailableException("Chưa cấu hình Security:EncryptionKey (AES-256, Base64).");
        try { var key = Convert.FromBase64String(encoded); if (key.Length == 32) return key; }
        catch (FormatException) { }
        throw new DependencyUnavailableException("Khóa mã hóa phải là 32 byte Base64.");
    }
    public string Encrypt(string value, string purpose)
    {
        var nonce = RandomNumberGenerator.GetBytes(12); var plaintext = Encoding.UTF8.GetBytes(value);
        var encrypted = new byte[plaintext.Length]; var tag = new byte[16];
        using var aes = new AesGcm(Key(), 16);
        aes.Encrypt(nonce, plaintext, encrypted, tag, Encoding.UTF8.GetBytes(purpose));
        return "v1:" + Convert.ToBase64String(nonce.Concat(tag).Concat(encrypted).ToArray());
    }
    public string Decrypt(string value, string purpose)
    {
        if (!value.StartsWith("v1:")) throw new CryptographicException("Định dạng ciphertext không hợp lệ.");
        var data = Convert.FromBase64String(value[3..]);
        if (data.Length < 28) throw new CryptographicException("Ciphertext không hợp lệ.");
        var plaintext = new byte[data.Length - 28];
        using var aes = new AesGcm(Key(), 16);
        aes.Decrypt(data.AsSpan(0,12), data.AsSpan(28), data.AsSpan(12,16), plaintext, Encoding.UTF8.GetBytes(purpose));
        return Encoding.UTF8.GetString(plaintext);
    }
}
