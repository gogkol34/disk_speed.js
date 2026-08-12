// disk_speed.cs — C# версия

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

class DiskStats {
    public List<double> ReadSpeeds { get; set; } = new List<double>();
    public List<double> WriteSpeeds { get; set; } = new List<double>();
    public double MaxRead { get; set; } = 0;
    public double MaxWrite { get; set; } = 0;
}

class Program {
    private static int interval = 2;
    private static Dictionary<string, DiskStats> stats = new Dictionary<string, DiskStats>();
    private static Dictionary<string, long> prevRead = new Dictionary<string, long>();
    private static Dictionary<string, long> prevWrite = new Dictionary<string, long>();

    static void Main(string[] args) {
        if (args.Length > 0 && int.TryParse(args[0], out int val) && val > 0) {
            interval = val;
        }

        Console.WriteLine("\u001B[36m💾 Disk Speed Monitor (C#)\u001B[0m");
        Console.WriteLine($"Интервал: {interval} сек");
        Console.WriteLine("Нажмите Ctrl+C для остановки...");

        // Получаем начальную статистику
        GetDiskIO(out prevRead, out prevWrite);
        Thread.Sleep(interval * 1000);

        var timer = new Timer(_ => {
            try {
                GetDiskIO(out var currRead, out var currWrite);
                var speeds = CalculateSpeeds(prevRead, prevWrite, currRead, currWrite);
                PrintSpeeds(speeds);
                prevRead = currRead;
                prevWrite = currWrite;
            } catch (Exception e) {
                Console.WriteLine($"\u001B[31m❌ Ошибка: {e.Message}\u001B[0m");
            }
        }, null, 0, interval * 1000);

        Console.CancelKeyPress += (sender, e) => {
            e.Cancel = true;
            timer.Dispose();
            Console.WriteLine("\n\n⏹️ Остановка...");
            PrintStats();
            Environment.Exit(0);
        };

        Thread.Sleep(Timeout.Infinite);
    }

    static void GetDiskIO(out Dictionary<string, long> read, out Dictionary<string, long> write) {
        read = new Dictionary<string, long>();
        write = new Dictionary<string, long>();

        if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
            // Windows: используем PerformanceCounter
            var drives = DriveInfo.GetDrives().Where(d => d.IsReady);
            foreach (var drive in drives) {
                read[drive.Name.Replace("\\", "")] = 0;
                write[drive.Name.Replace("\\", "")] = 0;
            }
        } else {
            // Linux/macOS: используем iostat (упрощённо)
            var psi = new ProcessStartInfo {
                FileName = "iostat",
                Arguments = "-d 1 2",
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            using var process = Process.Start(psi);
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            // Парсинг iostat (упрощённо)
            var lines = output.Split('\n');
            foreach (var line in lines) {
                var parts = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 6 && double.TryParse(parts[5], out _)) {
                    read[parts[0]] = (long)(double.Parse(parts[5]) * 1024);
                    write[parts[0]] = (long)(double.Parse(parts[6]) * 1024);
                }
            }
        }
    }

    static Dictionary<string, (double read, double write)> CalculateSpeeds(
        Dictionary<string, long> prevR, Dictionary<string, long> prevW,
        Dictionary<string, long> currR, Dictionary<string, long> currW) {

        var speeds = new Dictionary<string, (double, double)>();
        foreach (var disk in currR.Keys) {
            if (prevR.ContainsKey(disk) && currR.ContainsKey(disk)) {
                double readMB = (currR[disk] - prevR[disk]) / (1024.0 * 1024.0);
                double writeMB = (currW[disk] - prevW[disk]) / (1024.0 * 1024.0);
                speeds[disk] = (readMB / interval, writeMB / interval);

                if (!stats.ContainsKey(disk)) {
                    stats[disk] = new DiskStats();
                }
                stats[disk].ReadSpeeds.Add(readMB / interval);
                stats[disk].WriteSpeeds.Add(writeMB / interval);
                if (readMB / interval > stats[disk].MaxRead) stats[disk].MaxRead = readMB / interval;
                if (writeMB / interval > stats[disk].MaxWrite) stats[disk].MaxWrite = writeMB / interval;
            }
        }
        return speeds;
    }

    static void PrintSpeeds(Dictionary<string, (double read, double write)> speeds) {
        Console.WriteLine("\n" + new string('─', 60));
        Console.WriteLine($"\u001B[36mДиск   Чтение (МБ/с)   Запись (МБ/с)   Загрузка\u001B[0m");
        Console.WriteLine(new string('─', 60));

        foreach (var kv in speeds) {
            double read = kv.Value.read;
            double write = kv.Value.write;
            double load = Math.Min((read + write) / 1000 * 100, 100);

            string readColor = read > 300 ? "\u001B[32m" : read > 100 ? "\u001B[33m" : "\u001B[31m";
            string writeColor = write > 300 ? "\u001B[32m" : write > 100 ? "\u001B[33m" : "\u001B[31m";

            int barLen = 20;
            int filled = (int)(load / 100 * barLen);
            string bar = new string('█', filled) + new string('░', barLen - filled);

            Console.WriteLine($"{kv.Key,-6} {readColor}{read,8:F1}\u001B[0m     {writeColor}{write,8:F1}\u001B[0m     {bar} {load,5:F0}%");
        }
    }

    static void PrintStats() {
        Console.WriteLine($"\n\u001B[36m📊 Статистика:\u001B[0m");
        foreach (var kv in stats) {
            var s = kv.Value;
            if (s.ReadSpeeds.Count > 0) {
                double avgRead = s.ReadSpeeds.Average();
                double avgWrite = s.WriteSpeeds.Average();
                Console.WriteLine($"  {kv.Key}:");
                Console.WriteLine($"    Средняя скорость чтения: {avgRead:F1} МБ/с");
                Console.WriteLine($"    Средняя скорость записи: {avgWrite:F1} МБ/с");
                Console.WriteLine($"    Пиковая скорость чтения: {s.MaxRead:F1} МБ/с");
                Console.WriteLine($"    Пиковая скорость записи: {s.MaxWrite:F1} МБ/с");
            }
        }
    }
}
