using System.Security.Cryptography;
using System.Text;

namespace NCronJob;


internal static class StringExtensions
{
    public static string GenerateConsistentShortHash(this string input)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var base64ShortHash = Convert.ToBase64String(hashBytes).AsSpan()[..8];
        return RemoveChars(base64ShortHash, '+', '/', '=');
    }

    public static string RemoveChars(this ReadOnlySpan<char> input, params ReadOnlySpan<char> charsToRemove)
    {
        Span<char> buffer = stackalloc char[input.Length];
        var index = 0;

        foreach (var ch in input)
        {
            if (!charsToRemove.Contains(ch))
            {
                buffer[index++] = ch;
            }
        }

        return new string(buffer[..index]);
    }
}


