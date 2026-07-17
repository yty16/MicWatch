using System;

namespace MicWatch.Core;

internal static class Ownership
{
    private static readonly byte[] Mask = { 0x5A, 0x3C, 0x71, 0x12, 0x44, 0x29 };
    private static readonly byte[] Primary = { 0x23, 0x48, 0x08, 0x23, 0x72 };
    private static readonly byte[] Secondary = { 0x23, 0x48, 0x08, 0x25, 0x7C, 0x18, 0x6C };

    public static string Author => Reveal(Primary);

    public static string CoAuthor => Reveal(Secondary);

    public static bool Verify()
        => Author.Length == Primary.Length && CoAuthor.Length == Secondary.Length;

    public static void AssertLoaded()
    {
        if (!Verify())
            throw new InvalidOperationException();
    }

    private static string Reveal(byte[] data)
    {
        var chars = new char[data.Length];
        for (var i = 0; i < data.Length; i++)
            chars[i] = (char)(data[i] ^ Mask[i % Mask.Length]);
        return new string(chars);
    }
}
