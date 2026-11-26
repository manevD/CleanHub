using System.Text.Json;

namespace CleanHub.Services.OnlinePayments
{
    /// <summary>
    /// Чува ги прочитаните онлајн уплати во App_Data/online-payments.json
    /// (без промени во базата). Singleton, безбеден за повеќе нишки.
    /// </summary>
    public class OnlinePaymentStore
    {
        private static readonly JsonSerializerOptions Json = new()
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private readonly string _path;
        private readonly object _lock = new();
        private OnlinePaymentStoreData? _data;

        public OnlinePaymentStore(IWebHostEnvironment env)
        {
            var dir = Path.Combine(env.ContentRootPath, "App_Data");
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "online-payments.json");
        }

        private OnlinePaymentStoreData Load()
        {
            if (_data != null) return _data;
            try
            {
                _data = File.Exists(_path)
                    ? JsonSerializer.Deserialize<OnlinePaymentStoreData>(File.ReadAllText(_path), Json) ?? new()
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

        /// <summary>
        /// Запишува на диск. На Windows датотеката знае накратко да биде заклучена
        /// (антивирус, OneDrive, Visual Studio), па се пробува повеќе пати.
        /// Ако не успее, податоците остануваат во меморија и ќе се запишат при следното зачувување —
        /// страната не паѓа.
        /// </summary>
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
                        // Отстрани „само за читање“ ако е поставено
                        var attr = File.GetAttributes(_path);
                        if (attr.HasFlag(FileAttributes.ReadOnly)) File.SetAttributes(_path, attr & ~FileAttributes.ReadOnly);
                    }
                    File.Move(tmp, _path, true);
                    return;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Резервно: директно запишување во датотеката (без преместување)
                    try { File.WriteAllText(_path, json); TryDelete(tmp); return; }
                    catch (Exception ex2) when (ex2 is IOException or UnauthorizedAccessException)
                    {
                        if (attempt == 5)
                        {
                            System.Diagnostics.Debug.WriteLine("OnlinePaymentStore: не може да се запише " + _path + ": " + ex2.Message);
                            TryDelete(tmp);
                            return;
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

        public OnlinePaymentStoreData Snapshot()
        {
            lock (_lock)
            {
                var d = Load();
                return new OnlinePaymentStoreData
                {
                    LastRunUtc = d.LastRunUtc,
                    LastError = d.LastError,
                    LastRunAdded = d.LastRunAdded,
                    Payments = d.Payments.Select(Clone).ToList()
                };
            }
        }

        /// <summary>Додава нови уплати (дупликатите по клуч се прескокнуваат). Враќа колку се додадени.</summary>
        public int AddRange(IEnumerable<OnlinePayment> payments)
        {
            lock (_lock)
            {
                var d = Load();
                var keys = d.Payments.Select(p => p.Key).ToHashSet();
                var added = 0;
                foreach (var p in payments)
                {
                    if (string.IsNullOrWhiteSpace(p.Key) || !keys.Add(p.Key)) continue;
                    d.Payments.Add(p);
                    added++;
                }
                if (added > 0) Save();
                return added;
            }
        }

        public void SetRun(DateTime utc, int added, string? error)
        {
            lock (_lock)
            {
                var d = Load();
                if (error == null) d.LastRunUtc = utc;
                d.LastRunAdded = added;
                d.LastError = error;
                Save();
            }
        }

        public bool SetStatus(string key, OnlinePaymentStatus status, string? user)
        {
            lock (_lock)
            {
                var p = Load().Payments.FirstOrDefault(x => x.Key == key);
                if (p == null) return false;
                p.Status = status;
                p.ProcessedAt = status == OnlinePaymentStatus.New ? null : DateTime.UtcNow;
                p.ProcessedBy = status == OnlinePaymentStatus.New ? null : user;
                Save();
                return true;
            }
        }

        /// <summary>Менува статус на повеќе уплати со едно запишување.</summary>
        public int SetStatusMany(IEnumerable<string> keys, OnlinePaymentStatus status, string? user)
        {
            lock (_lock)
            {
                var set = keys.ToHashSet();
                var changed = 0;
                foreach (var p in Load().Payments.Where(x => set.Contains(x.Key)))
                {
                    p.Status = status;
                    p.ProcessedAt = status == OnlinePaymentStatus.New ? null : DateTime.UtcNow;
                    p.ProcessedBy = status == OnlinePaymentStatus.New ? null : user;
                    changed++;
                }
                if (changed > 0) Save();
                return changed;
            }
        }

        private static OnlinePayment Clone(OnlinePayment p) =>
            JsonSerializer.Deserialize<OnlinePayment>(JsonSerializer.Serialize(p, Json), Json)!;
    }
}
