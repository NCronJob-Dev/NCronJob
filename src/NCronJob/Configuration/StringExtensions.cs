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

    public static string RemoveChars(this ReadOnlySpan<char> input, params char[] charsToRemove)
    {
        Span<char> buffer = stackalloc char[input.Length];
        var index = 0;

        foreach (var ch in input)
        {
            var remove = false;
            foreach (var c in charsToRemove)
            {
                if (ch != c) continue;
                remove = true;
                break;
            }
            if (!remove)
            {
                buffer[index++] = ch;
            }
        }
        return new string(buffer[..index]);
    }
}


