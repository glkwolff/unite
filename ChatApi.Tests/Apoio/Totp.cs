using System.Security.Cryptography;

namespace ChatApi.Tests.Apoio;

/// <summary>
/// Faz o papel do Google Authenticator: gera o codigo de 6 digitos (RFC 6238, SHA-1, 30 s)
/// a partir da chave Base32 que a API devolve no QR code.
/// </summary>
public static class Totp
{
    private const string Alfabeto = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Codigo(string chaveBase32, DateTime? agora = null)
    {
        var chave = DeBase32(chaveBase32);
        var segundos = (long)((agora ?? DateTime.UtcNow) - DateTime.UnixEpoch).TotalSeconds;
        var passo = BitConverter.GetBytes(segundos / 30);
        if (BitConverter.IsLittleEndian) Array.Reverse(passo);

        var hash = HMACSHA1.HashData(chave, passo);
        var offset = hash[^1] & 0x0F;
        var binario = ((hash[offset] & 0x7F) << 24)
                    | (hash[offset + 1] << 16)
                    | (hash[offset + 2] << 8)
                    | hash[offset + 3];

        return (binario % 1_000_000).ToString("D6");
    }

    /// <summary>Um codigo que com certeza nao bate com o atual.</summary>
    public static string CodigoErrado(string chaveBase32)
    {
        var certo = int.Parse(Codigo(chaveBase32));
        return ((certo + 500_000) % 1_000_000).ToString("D6");
    }

    private static byte[] DeBase32(string texto)
    {
        texto = texto.TrimEnd('=').ToUpperInvariant();
        var bytes = new List<byte>(texto.Length * 5 / 8);
        int buffer = 0, bits = 0;

        foreach (var c in texto)
        {
            var valor = Alfabeto.IndexOf(c);
            if (valor < 0) throw new FormatException($"Caractere invalido em Base32: '{c}'.");

            buffer = (buffer << 5) | valor;
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                bytes.Add((byte)((buffer >> bits) & 0xFF));
            }
        }

        return bytes.ToArray();
    }
}
