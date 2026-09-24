namespace HouseBills.Wpf.Services;

/// <summary>Writes exported files, abstracted so ViewModels stay testable.</summary>
public interface IFileSaver
{
    /// <summary>Writes <paramref name="text"/> as UTF-8 with a byte order mark (so Excel shows č, ć… correctly), replacing the file.</summary>
    Task SaveTextAsync(string path, string text, CancellationToken cancellationToken);
}