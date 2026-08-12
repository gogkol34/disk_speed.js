

### 1. `disk_speed.py` (Python)

```python
# disk_speed.py — Python версия

import psutil
import time
import os
from datetime import datetime
from colorama import init, Fore, Style

init(autoreset=True)

class DiskSpeedMonitor:
    def __init__(self, interval=2):
        self.interval = interval
        self.stats = {}

    def get_disk_io(self):
        """Получает текущую статистику дисков"""
        io = psutil.disk_io_counters(perdisk=True)
        result = {}
        for disk, counters in io.items():
            result[disk] = {
                'read_bytes': counters.read_bytes,
                'write_bytes': counters.write_bytes,
                'read_time': counters.read_time,
                'write_time': counters.write_time,
            }
        return result

    def calculate_speed(self, prev, curr, interval):
        """Вычисляет скорость в МБ/с"""
        speeds = {}
        for disk in curr:
            if disk in prev:
                read_mb = (curr[disk]['read_bytes'] - prev[disk]['read_bytes']) / (1024 * 1024)
                write_mb = (curr[disk]['write_bytes'] - prev[disk]['write_bytes']) / (1024 * 1024)
                speeds[disk] = {
                    'read': read_mb / interval,
                    'write': write_mb / interval,
                }
                # Обновляем статистику
                if disk not in self.stats:
                    self.stats[disk] = {'read': [], 'write': [], 'max_read': 0, 'max_write': 0}
                self.stats[disk]['read'].append(read_mb / interval)
                self.stats[disk]['write'].append(write_mb / interval)
                if read_mb / interval > self.stats[disk]['max_read']:
                    self.stats[disk]['max_read'] = read_mb / interval
                if write_mb / interval > self.stats[disk]['max_write']:
                    self.stats[disk]['max_write'] = write_mb / interval
        return speeds

    def print_speeds(self, speeds):
        """Выводит скорости в таблице"""
        print("\n" + "─" * 60)
        print(f"{Fore.CYAN}Диск   Чтение (МБ/с)   Запись (МБ/с)   Загрузка{Style.RESET_ALL}")
        print("─" * 60)

        for disk, speed in speeds.items():
            read = speed.get('read', 0)
            write = speed.get('write', 0)
            load = (read + write) / 1000 * 100 if (read + write) > 0 else 0
            load = min(load, 100)

            # Цвет для скорости
            read_color = Fore.GREEN if read > 300 else Fore.YELLOW if read > 100 else Fore.RED
            write_color = Fore.GREEN if write > 300 else Fore.YELLOW if write > 100 else Fore.RED

            # Простой прогресс-бар
            bar_length = 20
            filled = int(load / 100 * bar_length)
            bar = '█' * filled + '░' * (bar_length - filled)

            print(f"{disk:<6} {read_color}{read:>8.1f}{Style.RESET_ALL}     {write_color}{write:>8.1f}{Style.RESET_ALL}     {bar} {load:>5.0f}%")

    def print_stats(self):
        """Выводит общую статистику"""
        print(f"\n{Fore.CYAN}📊 Статистика:{Style.RESET_ALL}")
        for disk, stat in self.stats.items():
            if stat['read']:
                avg_read = sum(stat['read']) / len(stat['read'])
                avg_write = sum(stat['write']) / len(stat['write'])
                print(f"  {disk}:")
                print(f"    Средняя скорость чтения: {avg_read:.1f} МБ/с")
                print(f"    Средняя скорость записи: {avg_write:.1f} МБ/с")
                print(f"    Пиковая скорость чтения: {stat['max_read']:.1f} МБ/с")
                print(f"    Пиковая скорость записи: {stat['max_write']:.1f} МБ/с")

    def run(self):
        print(f"{Fore.CYAN}💾 Disk Speed Monitor (Python){Style.RESET_ALL}")
        print(f"Интервал: {self.interval} сек")
        print("Нажмите Ctrl+C для остановки...")

        prev = self.get_disk_io()
        time.sleep(self.interval)

        try:
            while True:
                curr = self.get_disk_io()
                speeds = self.calculate_speed(prev, curr, self.interval)
                self.print_speeds(speeds)
                prev = curr
                time.sleep(self.interval)
        except KeyboardInterrupt:
            print("\n\n⏹️ Остановка...")
            self.print_stats()

def main():
    interval = 2
    if os.environ.get('DISK_INTERVAL'):
        try:
            interval = int(os.environ['DISK_INTERVAL'])
        except:
            pass
    monitor = DiskSpeedMonitor(interval)
    monitor.run()

if __name__ == "__main__":
    main()
