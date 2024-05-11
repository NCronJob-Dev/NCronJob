using System.Security.Cryptography;
using System.Text;

namespace LinkDotNet.NCronJob;

internal static class StringExtensions
{
    public static string GenerateConsistentShortHash(this string input)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hashBytes)[..8]
            .RemoveChar('+')
            .RemoveChar('/')
            .RemoveChar('=');
    }

    public static string RemoveChar(this string input, char charToRemove)
    {
        Span<char> buffer = stackalloc char[input.Length];
        var index = 0;
        foreach (var ch in input)
        {
            if (ch != charToRemove)
            {
                buffer[index++] = ch;
            }
        }
        return new string(buffer[..index]);
    }

}
