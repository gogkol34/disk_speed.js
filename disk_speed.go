// disk_speed.go — Go версия

package main

import (
	"fmt"
	"os"
	"os/signal"
	"strconv"
	"syscall"
	"time"

	"github.com/shirou/gopsutil/v3/disk"
)

type DiskSpeedMonitor struct {
	interval time.Duration
	stats    map[string]*DiskStats
}

type DiskStats struct {
	ReadSpeeds  []float64
	WriteSpeeds []float64
	MaxRead     float64
	MaxWrite    float64
}

func NewDiskSpeedMonitor(interval time.Duration) *DiskSpeedMonitor {
	return &DiskSpeedMonitor{
		interval: interval,
		stats:    make(map[string]*DiskStats),
	}
}

func (m *DiskSpeedMonitor) getDiskIO() (map[string]disk.IOCountersStat, error) {
	io, err := disk.IOCounters()
	if err != nil {
		return nil, err
	}
	return io, nil
}

func (m *DiskSpeedMonitor) calculateSpeed(prev, curr map[string]disk.IOCountersStat, interval float64) map[string]map[string]float64 {
	speeds := make(map[string]map[string]float64)
	for diskName, currStats := range curr {
		if prevStats, ok := prev[diskName]; ok {
			readMB := float64(currStats.ReadBytes-prevStats.ReadBytes) / (1024 * 1024)
			writeMB := float64(currStats.WriteBytes-prevStats.WriteBytes) / (1024 * 1024)
			readSpeed := readMB / interval
			writeSpeed := writeMB / interval

			speeds[diskName] = map[string]float64{
				"read":  readSpeed,
				"write": writeSpeed,
			}

			if _, ok := m.stats[diskName]; !ok {
				m.stats[diskName] = &DiskStats{
					ReadSpeeds:  []float64{},
					WriteSpeeds: []float64{},
				}
			}
			m.stats[diskName].ReadSpeeds = append(m.stats[diskName].ReadSpeeds, readSpeed)
			m.stats[diskName].WriteSpeeds = append(m.stats[diskName].WriteSpeeds, writeSpeed)
			if readSpeed > m.stats[diskName].MaxRead {
				m.stats[diskName].MaxRead = readSpeed
			}
			if writeSpeed > m.stats[diskName].MaxWrite {
				m.stats[diskName].MaxWrite = writeSpeed
			}
		}
	}
	return speeds
}

func (m *DiskSpeedMonitor) printSpeeds(speeds map[string]map[string]float64) {
	fmt.Println("\n" + "─"*60)
	fmt.Printf("\x1b[36mДиск   Чтение (МБ/с)   Запись (МБ/с)   Загрузка\x1b[0m\n")
	fmt.Println("─" * 60)

	for disk, speed := range speeds {
		read := speed["read"]
		write := speed["write"]
		load := (read + write) / 1000 * 100
		if load > 100 {
			load = 100
		}

		readColor := "\x1b[32m"
		if read < 100 {
			readColor = "\x1b[31m"
		} else if read < 300 {
			readColor = "\x1b[33m"
		}

		writeColor := "\x1b[32m"
		if write < 100 {
			writeColor = "\x1b[31m"
		} else if write < 300 {
			writeColor = "\x1b[33m"
		}

		barLen := 20
		filled := int(load / 100 * float64(barLen))
		bar := ""
		for i := 0; i < filled; i++ {
			bar += "█"
		}
		for i := filled; i < barLen; i++ {
			bar += "░"
		}

		fmt.Printf("%-6s %s%8.1f\x1b[0m     %s%8.1f\x1b[0m     %s %5.0f%%\n",
			disk, readColor, read, writeColor, write, bar, load)
	}
}

func (m *DiskSpeedMonitor) printStats() {
	fmt.Printf("\n\x1b[36m📊 Статистика:\x1b[0m\n")
	for disk, stats := range m.stats {
		if len(stats.ReadSpeeds) > 0 {
			avgRead := 0.0
			avgWrite := 0.0
			for _, v := range stats.ReadSpeeds {
				avgRead += v
			}
			avgRead /= float64(len(stats.ReadSpeeds))
			for _, v := range stats.WriteSpeeds {
				avgWrite += v
			}
			avgWrite /= float64(len(stats.WriteSpeeds))

			fmt.Printf("  %s:\n", disk)
			fmt.Printf("    Средняя скорость чтения: %.1f МБ/с\n", avgRead)
			fmt.Printf("    Средняя скорость записи: %.1f МБ/с\n", avgWrite)
			fmt.Printf("    Пиковая скорость чтения: %.1f МБ/с\n", stats.MaxRead)
			fmt.Printf("    Пиковая скорость записи: %.1f МБ/с\n", stats.MaxWrite)
		}
	}
}

func (m *DiskSpeedMonitor) run() {
	fmt.Printf("\x1b[36m💾 Disk Speed Monitor (Go)\x1b[0m\n")
	fmt.Printf("Интервал: %.0f сек\n", m.interval.Seconds())
	fmt.Println("Нажмите Ctrl+C для остановки...")

	prev, err := m.getDiskIO()
	if err != nil {
		fmt.Printf("\x1b[31m❌ Ошибка: %v\x1b[0m\n", err)
		return
	}
	time.Sleep(m.interval)

	sigChan := make(chan os.Signal, 1)
	signal.Notify(sigChan, syscall.SIGINT, syscall.SIGTERM)

	ticker := time.NewTicker(m.interval)
	defer ticker.Stop()

	for {
		select {
		case <-ticker.C:
			curr, err := m.getDiskIO()
			if err != nil {
				fmt.Printf("\x1b[31m❌ Ошибка: %v\x1b[0m\n", err)
				continue
			}
			speeds := m.calculateSpeed(prev, curr, m.interval.Seconds())
			m.printSpeeds(speeds)
			prev = curr
		case <-sigChan:
			fmt.Println("\n\n⏹️ Остановка...")
			m.printStats()
			return
		}
	}
}

func main() {
	interval := 2
	if len(os.Args) > 1 {
		if val, err := strconv.Atoi(os.Args[1]); err == nil && val > 0 {
			interval = val
		}
	}
	monitor := NewDiskSpeedMonitor(time.Duration(interval) * time.Second)
	monitor.run()
}
