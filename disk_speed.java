// disk_speed.java — Java версия

import oshi.SystemInfo;
import oshi.hardware.HardwareAbstractionLayer;
import oshi.hardware.DiskDrive;
import oshi.hardware.DiskStore;

import java.util.*;
import java.util.concurrent.*;

public class disk_speed {
    private static final int INTERVAL = 2;
    private static Map<String, DiskStats> stats = new HashMap<>();

    static class DiskStats {
        List<Double> readSpeeds = new ArrayList<>();
        List<Double> writeSpeeds = new ArrayList<>();
        double maxRead = 0;
        double maxWrite = 0;
    }

    public static void main(String[] args) throws InterruptedException {
        System.out.println("\u001B[36m💾 Disk Speed Monitor (Java)\u001B[0m");
        System.out.println("Интервал: " + INTERVAL + " сек");
        System.out.println("Нажмите Ctrl+C для остановки...");

        SystemInfo si = new SystemInfo();
        HardwareAbstractionLayer hal = si.getHardware();
        List<DiskStore> disks = hal.getDiskStores();

        Map<String, Long> prevRead = new HashMap<>();
        Map<String, Long> prevWrite = new HashMap<>();

        for (DiskStore disk : disks) {
            prevRead.put(disk.getName(), disk.getReadBytes());
            prevWrite.put(disk.getName(), disk.getWriteBytes());
        }

        Thread.sleep(INTERVAL * 1000);

        ScheduledExecutorService executor = Executors.newSingleThreadScheduledExecutor();
        executor.scheduleAtFixedRate(() -> {
            try {
                HardwareAbstractionLayer hal2 = si.getHardware();
                List<DiskStore> disks2 = hal2.getDiskStores();

                Map<String, Double> speeds = new HashMap<>();
                Map<String, Long> currRead = new HashMap<>();
                Map<String, Long> currWrite = new HashMap<>();

                for (DiskStore disk : disks2) {
                    currRead.put(disk.getName(), disk.getReadBytes());
                    currWrite.put(disk.getName(), disk.getWriteBytes());
                }

                System.out.println("\n" + "─".repeat(60));
                System.out.printf("\u001B[36m%-6s %-15s %-15s %-10s\u001B[0m\n", "Диск", "Чтение (МБ/с)", "Запись (МБ/с)", "Загрузка");
                System.out.println("─".repeat(60));

                for (DiskStore disk : disks2) {
                    String name = disk.getName();
                    if (prevRead.containsKey(name) && currRead.containsKey(name)) {
                        double readMB = (currRead.get(name) - prevRead.get(name)) / (1024.0 * 1024.0);
                        double writeMB = (currWrite.get(name) - prevWrite.get(name)) / (1024.0 * 1024.0);
                        double readSpeed = readMB / INTERVAL;
                        double writeSpeed = writeMB / INTERVAL;
                        double load = Math.min((readSpeed + writeSpeed) / 1000 * 100, 100);

                        String readColor = readSpeed > 300 ? "\u001B[32m" : readSpeed > 100 ? "\u001B[33m" : "\u001B[31m";
                        String writeColor = writeSpeed > 300 ? "\u001B[32m" : writeSpeed > 100 ? "\u001B[33m" : "\u001B[31m";

                        int barLen = 20;
                        int filled = (int) (load / 100 * barLen);
                        StringBuilder bar = new StringBuilder();
                        for (int i = 0; i < filled; i++) bar.append("█");
                        for (int i = filled; i < barLen; i++) bar.append("░");

                        System.out.printf("%-6s %s%8.1f\u001B[0m     %s%8.1f\u001B[0m     %s %5.0f%%\n",
                            name, readColor, readSpeed, writeColor, writeSpeed, bar.toString(), load);

                        if (!stats.containsKey(name)) {
                            stats.put(name, new DiskStats());
                        }
                        DiskStats ds = stats.get(name);
                        ds.readSpeeds.add(readSpeed);
                        ds.writeSpeeds.add(writeSpeed);
                        if (readSpeed > ds.maxRead) ds.maxRead = readSpeed;
                        if (writeSpeed > ds.maxWrite) ds.maxWrite = writeSpeed;
                    }
                }

                prevRead = currRead;
                prevWrite = currWrite;
            } catch (Exception e) {
                System.err.println("\u001B[31m❌ Ошибка: " + e.getMessage() + "\u001B[0m");
            }
        }, 0, INTERVAL, TimeUnit.SECONDS);

        Thread.sleep(30000); // 30 секунд
        executor.shutdown();
        executor.awaitTermination(5, TimeUnit.SECONDS);

        System.out.println("\n\n\u001B[36m📊 Статистика:\u001B[0m");
        for (Map.Entry<String, DiskStats> entry : stats.entrySet()) {
            DiskStats ds = entry.getValue();
            if (!ds.readSpeeds.isEmpty()) {
                double avgRead = ds.readSpeeds.stream().mapToDouble(Double::doubleValue).average().orElse(0);
                double avgWrite = ds.writeSpeeds.stream().mapToDouble(Double::doubleValue).average().orElse(0);
                System.out.printf("  %s:\n", entry.getKey());
                System.out.printf("    Средняя скорость чтения: %.1f МБ/с\n", avgRead);
                System.out.printf("    Средняя скорость записи: %.1f МБ/с\n", avgWrite);
                System.out.printf("    Пиковая скорость чтения: %.1f МБ/с\n", ds.maxRead);
                System.out.printf("    Пиковая скорость записи: %.1f МБ/с\n", ds.maxWrite);
            }
        }
    }
}
