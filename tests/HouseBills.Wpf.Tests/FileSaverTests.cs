using System.Text;

using HouseBills.Wpf.Services;

namespace HouseBills.Wpf.Tests;

public sealed class FileSaverTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"HouseBillsExport_{Guid.NewGuid():N}.csv");

    public void Dispose() => File.Delete(_path);

    [Fact]
    public async Task SaveTextAsync_Text_WritesUtf8WithByteOrderMarkForExcel()
    {
        await new FileSaver().SaveTextAsync(_path, "Opis;Iznos\r\nStruja čćž;4230,50\r\n", TestContext.Current.CancellationToken);

        var bytes = await File.ReadAllBytesAsync(_path, TestContext.Current.CancellationToken);
        bytes.Take(3).ShouldBe(new byte[] { 0xEF, 0xBB, 0xBF });
        Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3).ShouldBe("Opis;Iznos\r\nStruja čćž;4230,50\r\n");
    }

    [Fact]
    public async Task SaveTextAsync_ExistingFile_IsReplaced()
    {
        await File.WriteAllTextAsync(_path, "old content that is longer than the new one", TestContext.Current.CancellationToken);

        await new FileSaver().SaveTextAsync(_path, "new", TestContext.Current.CancellationToken);

        (await File.ReadAllTextAsync(_path, TestContext.Current.CancellationToken)).ShouldBe("new");
    }
}