using System.Text.Json;

namespace CleanHub.Services.Ai
{
    /// <summary>
    /// Чува ги генерираните годишни извештаи во App_Data/annual-reports.json
    /// (без промени во базата), за да можат подоцна да се отворат, менуваат, печатат или бришат.
    /// Singleton, безбеден за повеќе нишки.
    /// </summary>
    public class AnnualReportStore
    {
        private static readonly JsonSerializerOptions Json = new()
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private readonly string _path;
        private readonly object _lock = new();
        private List<AnnualReport>? _data;

        public AnnualReportStore(IWebHostEnvironment env)
        {
            var dir = Path.Combine(env.ContentRootPath, "App_Data");
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "annual-reports.json");
        }

        private List<AnnualReport> Load()
        {
            if (_data != null) return _data;
            try
            {
                _data = File.Exists(_path)
                    ? JsonSerializer.Deserialize<List<AnnualReport>>(File.ReadAllText(_path), Json) ?? new()
                    : new();
            }
            catch
            {
                // Оштетена датотека — се чува копија и се почнува од ново
                try { File.Copy(_path, _path + ".broken-" + DateTime.UtcNow.Ticks, true); } catch { }
                _data = new();
            }
            return _data;
        }

        /// <summary>Запишува на диск (преку привремена датотека, со неколку обиди ако е заклучена).</summary>
        private void Save()
        {
            var json = JsonSerializer.Serialize(_data, Json);
            var tmp = _path + ".tmp";
            for (var attempt = 1; attempt <= 5; attempt++)
            {
                try
                {
                    File.WriteAllText(tmp, json);
                    if (File.Exists(_path))
                    {
                        var attr = File.GetAttributes(_path);
                        if (attr.HasFlag(FileAttributes.ReadOnly)) File.SetAttributes(_path, attr & ~FileAttributes.ReadOnly);
                    }
                    File.Move(tmp, _path, true);
                    return;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    try { File.WriteAllText(_path, json); TryDelete(tmp); return; }
                    catch (Exception ex2) when (ex2 is IOException or UnauthorizedAccessException)
                    {
                        if (attempt == 5)
                        {
                            TryDelete(tmp);
                            throw new IOException("Извештајот не може да се зачува на диск: " + ex2.Message, ex2);
                        }
                        Thread.Sleep(100 * attempt);
                    }
                }
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private static AnnualReport Clone(AnnualReport r) =>
            JsonSerializer.Deserialize<AnnualReport>(JsonSerializer.Serialize(r, Json), Json)!;

        /// <summary>Листа на зачувани извештаи (најновите прво), опционално за еден објект.</summary>
        public List<AnnualReport> List(int? buildingId = null)
        {
            lock (_lock)
            {
                return Load()
                    .Where(r => buildingId == null || r.Stats.BuildingId == buildingId)
                    .OrderByDescending(r => r.Stats.Year)
                    .ThenByDescending(r => r.UpdatedAt ?? r.GeneratedAt)
                    .Select(Clone)
                    .ToList();
            }
        }

        public AnnualReport? Get(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            lock (_lock)
            {
                var r = Load().FirstOrDefault(x => x.Id == id);
                return r == null ? null : Clone(r);
            }
        }

        /// <summary>Додава нов или го заменува постоечкиот извештај со истото Id.</summary>
        public void Upsert(AnnualReport report)
        {
            lock (_lock)
            {
                var list = Load();
                var copy = Clone(report);
                var idx = list.FindIndex(x => x.Id == report.Id);
                if (idx >= 0) list[idx] = copy; else list.Add(copy);
                Save();
            }
        }

        public bool Delete(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            lock (_lock)
            {
                var removed = Load().RemoveAll(x => x.Id == id) > 0;
                if (removed) Save();
                return removed;
            }
        }
    }
}
