namespace HouseBills.Wpf.Services;

/// <summary>Reads files to import, abstracted so ViewModels stay testable.</summary>
public interface IFileReader
{
    /// <summary>
    /// Reads a text file. UTF-8 (with or without a byte order mark) is read as such; anything else as the Windows
    /// regional code page, which is what Excel's plain "CSV" format saves in (e.g. Windows-1250 for č, ć, ž).
    /// </summary>
    Task<string> ReadTextAsync(string path, CancellationToken cancellationToken);
}