using System;
using System.Security.Cryptography;
using Volo.Abp.DependencyInjection;

namespace SayHello.ShortLink.ShortLinks;

public class RandomShortCodeGenerator : IShortCodeGenerator, ITransientDependency
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    public string Generate(int length)
    {
        if (length is < ShortLinkConsts.MinCodeLength or > ShortLinkConsts.MaxCodeLength)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        return string.Create(length, Alphabet, static (buffer, alphabet) =>
        {
            for (var index = 0; index < buffer.Length; index++)
            {
                buffer[index] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
            }
        });
    }
}
