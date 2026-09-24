using System.IO;
using System.Text;

using HouseBills.Wpf.Localization;

namespace HouseBills.Wpf.Services;

internal sealed class FileReader : IFileReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    static FileReader()
    {
        // Windows code pages such as 1250 are not available in .NET until this provider is registered.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public async Task<string> ReadTextAsync(string path, CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        return Decode(bytes);
    }

    internal static string Decode(byte[] bytes)
    {
        var preamble = Encoding.UTF8.Preamble;
        if (bytes.AsSpan().StartsWith(preamble))
        {
            return Encoding.UTF8.GetString(bytes, preamble.Length, bytes.Length - preamble.Length);
        }

        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(LocalizedStrings.RegionalCulture.TextInfo.ANSICodePage).GetString(bytes);
        }
    }
}