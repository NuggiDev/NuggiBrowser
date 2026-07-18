using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace nuggiUI
{
    public class HistoryEntry
    {
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }

    public static class HistoryManager
    {
        private static string _filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NuggiBrowser", "history.json");
        private static List<HistoryEntry> _history = new();
        private static bool _loaded = false;

        public static async Task LoadAsync()
        {
            try
            {
                var dir = Path.GetDirectoryName(_filePath);
                if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                if (File.Exists(_filePath))
                {
                    var json = await File.ReadAllTextAsync(_filePath);
                    _history = JsonSerializer.Deserialize<List<HistoryEntry>>(json) ?? new();
                }
                _loaded = true;
            }
            catch { _loaded = true; }
        }

        public static async Task AddEntryAsync(string title, string url)
        {
            if (url == "nuggi://newtab" || url == "about:blank" || string.IsNullOrWhiteSpace(url)) return;
            if (!_loaded) await LoadAsync();

            _history.Insert(0, new HistoryEntry { Title = title, Url = url, Timestamp = DateTime.Now });
            if (_history.Count > 1000) _history = _history.GetRange(0, 1000);

            await SaveAsync();
        }

        public static async Task ClearAsync()
        {
            _history.Clear();
            await SaveAsync();
        }

        public static List<HistoryEntry> GetHistory()
        {
            if (!_loaded) _ = LoadAsync();
            return _history;
        }

        private static async Task SaveAsync()
        {
            try
            {
                var json = JsonSerializer.Serialize(_history);
                await File.WriteAllTextAsync(_filePath, json);
            }
            catch { }
        }
    }
}
