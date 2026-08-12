<?php
// disk_speed.php — PHP версия

class DiskSpeedMonitor {
    private $interval;
    private $stats = [];
    private $prevRead = [];
    private $prevWrite = [];

    public function __construct($interval = 2) {
        $this->interval = $interval;
    }

    private function getDiskIO() {
        $read = [];
        $write = [];

        if (strtoupper(substr(PHP_OS, 0, 3)) === 'WIN') {
            // Windows: используем wmic
            $output = shell_exec('wmic diskdrive get DeviceID,Name,Size 2>nul');
            // Упрощённо: только системный диск
            $read['C:'] = 0;
            $write['C:'] = 0;
        } else {
            // Linux/macOS: используем iostat
            $output = shell_exec('iostat -d 1 2 2>/dev/null');
            $lines = explode("\n", $output);
            foreach ($lines as $line) {
                $parts = preg_split('/\s+/', trim($line));
                if (count($parts) >= 7 && is_numeric($parts[5])) {
                    $disk = $parts[0];
                    $read[$disk] = (int)($parts[5] * 1024);
                    $write[$disk] = (int)($parts[6] * 1024);
                }
            }
        }
        return [$read, $write];
    }

    private function calculateSpeeds($prevR, $prevW, $currR, $currW) {
        $speeds = [];
        foreach ($currR as $disk => $read) {
            if (isset($prevR[$disk])) {
                $readMB = ($read - $prevR[$disk]) / (1024 * 1024);
                $writeMB = ($currW[$disk] - $prevW[$disk]) / (1024 * 1024);
                $readSpeed = $readMB / $this->interval;
                $writeSpeed = $writeMB / $this->interval;

                $speeds[$disk] = ['read' => $readSpeed, 'write' => $writeSpeed];

                if (!isset($this->stats[$disk])) {
                    $this->stats[$disk] = ['read' => [], 'write' => [], 'max_read' => 0, 'max_write' => 0];
                }
                $this->stats[$disk]['read'][] = $readSpeed;
                $this->stats[$disk]['write'][] = $writeSpeed;
                if ($readSpeed > $this->stats[$disk]['max_read']) {
                    $this->stats[$disk]['max_read'] = $readSpeed;
                }
                if ($writeSpeed > $this->stats[$disk]['max_write']) {
                    $this->stats[$disk]['max_write'] = $writeSpeed;
                }
            }
        }
        return $speeds;
    }

    private function printSpeeds($speeds) {
        echo "\n" . str_repeat("─", 60) . "\n";
        echo "\033[36mДиск   Чтение (МБ/с)   Запись (МБ/с)   Загрузка\033[0m\n";
        echo str_repeat("─", 60) . "\n";

        foreach ($speeds as $disk => $speed) {
            $read = $speed['read'] ?? 0;
            $write = $speed['write'] ?? 0;
            $load = min(($read + $write) / 1000 * 100, 100);

            $readColor = $read > 300 ? "\033[32m" : ($read > 100 ? "\033[33m" : "\033[31m");
            $writeColor = $write > 300 ? "\033[32m" : ($write > 100 ? "\033[33m" : "\033[31m");

            $barLen = 20;
            $filled = (int)($load / 100 * $barLen);
            $bar = str_repeat("█", $filled) . str_repeat("░", $barLen - $filled);

            printf("%-6s %s%8.1f\033[0m     %s%8.1f\033[0m     %s %5.0f%%\n",
                $disk, $readColor, $read, $writeColor, $write, $bar, $load);
        }
    }

    private function printStats() {
        echo "\n\033[36m📊 Статистика:\033[0m\n";
        foreach ($this->stats as $disk => $stat) {
            if (!empty($stat['read'])) {
                $avgRead = array_sum($stat['read']) / count($stat['read']);
                $avgWrite = array_sum($stat['write']) / count($stat['write']);
                echo "  $disk:\n";
                echo "    Средняя скорость чтения: " . round($avgRead, 1) . " МБ/с\n";
                echo "    Средняя скорость записи: " . round($avgWrite, 1) . " МБ/с\n";
                echo "    Пиковая скорость чтения: " . round($stat['max_read'], 1) . " МБ/с\n";
                echo "    Пиковая скорость записи: " . round($stat['max_write'], 1) . " МБ/с\n";
            }
        }
    }

    public function run() {
        echo "\033[36m💾 Disk Speed Monitor (PHP)\033[0m\n";
        echo "Интервал: {$this->interval} сек\n";
        echo "Нажмите Ctrl+C для остановки...\n";

        pcntl_signal(SIGINT, function() {
            echo "\n\n⏹️ Остановка...\n";
            $this->printStats();
            exit(0);
        });

        list($this->prevRead, $this->prevWrite) = $this->getDiskIO();
        sleep($this->interval);

        while (true) {
            pcntl_signal_dispatch();
            list($currRead, $currWrite) = $this->getDiskIO();
            $speeds = $this->calculateSpeeds($this->prevRead, $this->prevWrite, $currRead, $currWrite);
            $this->printSpeeds($speeds);
            $this->prevRead = $currRead;
            $this->prevWrite = $currWrite;
            sleep($this->interval);
        }
    }
}

$interval = isset($argv[1]) ? (int)$argv[1] : 2;
$monitor = new DiskSpeedMonitor($interval);
$monitor->run();
?>
