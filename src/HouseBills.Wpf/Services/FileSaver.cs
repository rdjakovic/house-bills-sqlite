using System.IO;
using System.Text;

namespace HouseBills.Wpf.Services;

internal sealed class FileSaver : IFileSaver
{
    // Encoding.UTF8 writes the byte order mark; Excel needs it to read the file as UTF-8.
    public Task SaveTextAsync(string path, string text, CancellationToken cancellationToken) =>
        File.WriteAllTextAsync(path, text, Encoding.UTF8, cancellationToken);
}