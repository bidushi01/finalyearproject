using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace finalyearproject.Data.Security;

public sealed class ChatMessageProtector
{
    private const string Prefix = "PA1:";
    private readonly byte[]? _key;

    public ChatMessageProtector(IConfiguration configuration)
    {
        var b64 = configuration["ChatMessages:EncryptionKeyBase64"]?.Trim();
        if (string.IsNullOrEmpty(b64)) return;

        try
        {
            var k = Convert.FromBase64String(b64);
            _key = k.Length == 32 ? k : null;
        }
        catch
        {
            _key = null;
        }
    }

    public bool IsEnabled => _key != null;

    public string Protect(string? plainText)
    {
        if (_key == null || string.IsNullOrEmpty(plainText))
            return plainText ?? "";

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Key = _key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateIV();

        using var enc = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipher = enc.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var payload = new byte[aes.IV.Length + cipher.Length];
        Buffer.BlockCopy(aes.IV, 0, payload, 0, aes.IV.Length);
        Buffer.BlockCopy(cipher, 0, payload, aes.IV.Length, cipher.Length);

        return Prefix + Convert.ToBase64String(payload);
    }

    /// <summary>
    /// Decrypts values written by <see cref="Protect"/>; legacy rows without the prefix are returned unchanged.
    /// </summary>
    public string Unprotect(string? stored)
    {
        if (string.IsNullOrEmpty(stored))
            return "";

        if (!stored.StartsWith(Prefix, StringComparison.Ordinal))
            return stored;

        if (_key == null)
            return "[Encrypted message — set ChatMessages:EncryptionKeyBase64 to the same key used when the message was saved]";

        try
        {
            var raw = Convert.FromBase64String(stored[Prefix.Length..]);
            if (raw.Length <= 16)
                return "[Invalid message data]";

            var iv = new byte[16];
            var cipher = new byte[raw.Length - 16];
            Buffer.BlockCopy(raw, 0, iv, 0, 16);
            Buffer.BlockCopy(raw, 16, cipher, 0, cipher.Length);

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var dec = aes.CreateDecryptor();
            var plain = dec.TransformFinalBlock(cipher, 0, cipher.Length);
            return Encoding.UTF8.GetString(plain);
        }
        catch
        {
            return "[Message could not be decrypted]";
        }
    }
}
