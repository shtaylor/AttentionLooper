using System.IO;

namespace AttentionLooper.Models;

public sealed class SoundLibrary
{
    private readonly string _libraryDir;
    private readonly object _lock = new();
    private Dictionary<string, string> _sounds = new(StringComparer.OrdinalIgnoreCase);

    public SoundLibrary(string libraryDir)
    {
        _libraryDir = libraryDir;
        Refresh();
    }

    public void Refresh()
    {
        Directory.CreateDirectory(_libraryDir);

        var files = Directory.EnumerateFiles(_libraryDir, "*.*", SearchOption.TopDirectoryOnly)
            .Where(p =>
            {
                var ext = Path.GetExtension(p);
                return ext.Equals(".mp3", StringComparison.OrdinalIgnoreCase)
                    || ext.Equals(".wav", StringComparison.OrdinalIgnoreCase);
            });

        var newSounds = files
            .Select(p => new { Name = Path.GetFileNameWithoutExtension(p), Path = p })
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Path, StringComparer.OrdinalIgnoreCase);

        lock (_lock)
        {
            _sounds = newSounds;
        }
    }

    public IReadOnlyList<string> GetSoundNamesSorted()
    {
        lock (_lock)
        {
            return _sounds.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }

    public bool TryGetPath(string name, out string path)
    {
        lock (_lock)
        {
            return _sounds.TryGetValue(name, out path!);
        }
    }

    public string? GetFirstAvailableName()
    {
        lock (_lock)
        {
            return _sounds.Keys
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }
    }

    public bool HasAny
    {
        get
        {
            lock (_lock)
            {
                return _sounds.Count > 0;
            }
        }
    }
}
