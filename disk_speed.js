// disk_speed.js — JavaScript версия

const { exec } = require('child_process');
const os = require('os');

class DiskSpeedMonitor {
    constructor(interval = 2) {
        this.interval = interval;
        this.stats = {};
        this.prevData = null;
    }

    getDiskIO() {
        return new Promise((resolve, reject) => {
            if (os.platform() === 'win32') {
                // Windows: используем wmic
                exec('wmic diskdrive get DeviceID,Name,Size', (err, stdout) => {
                    if (err) reject(err);
                    // Упрощённо: возвращаем заглушку
                    resolve({
                        'C:': { read_bytes: Math.random() * 100000000, write_bytes: Math.random() * 50000000 },
                        'D:': { read_bytes: Math.random() * 80000000, write_bytes: Math.random() * 40000000 }
                    });
                });
            } else {
                // Linux/macOS: используем iostat
                exec('iostat -d 1 2', (err, stdout) => {
                    if (err) reject(err);
                    // Упрощённо: парсим iostat
                    const lines = stdout.split('\n');
                    const result = {};
                    for (const line of lines) {
                        const parts = line.trim().split(/\s+/);
                        if (parts.length >= 6 && !isNaN(parts[5])) {
                            const disk = parts[0];
                            result[disk] = {
                                read_bytes: parseFloat(parts[5]) * 1024,
                                write_bytes: parseFloat(parts[6]) * 1024
                            };
                        }
                    }
                    resolve(result);
                });
            }
        });
    }

    calculateSpeed(prev, curr, interval) {
        const speeds = {};
        for (const disk of Object.keys(curr)) {
            if (prev[disk]) {
                const readMB = (curr[disk].read_bytes - prev[disk].read_bytes) / (1024 * 1024);
                const writeMB = (curr[disk].write_bytes - prev[disk].write_bytes) / (1024 * 1024);
                speeds[disk] = {
                    read: readMB / interval,
                    write: writeMB / interval
                };
                if (!this.stats[disk]) {
                    this.stats[disk] = { read: [], write: [], maxRead: 0, maxWrite: 0 };
                }
                this.stats[disk].read.push(readMB / interval);
                this.stats[disk].write.push(writeMB / interval);
                if (readMB / interval > this.stats[disk].maxRead) {
                    this.stats[disk].maxRead = readMB / interval;
                }
                if (writeMB / interval > this.stats[disk].maxWrite) {
                    this.stats[disk].maxWrite = writeMB / interval;
                }
            }
        }
        return speeds;
    }

    printSpeeds(speeds) {
        console.log('\n' + '─'.repeat(60));
        console.log('\x1b[36mДиск   Чтение (МБ/с)   Запись (МБ/с)   Загрузка\x1b[0m');
        console.log('─'.repeat(60));

        for (const [disk, speed] of Object.entries(speeds)) {
            const read = speed.read || 0;
            const write = speed.write || 0;
            const load = Math.min((read + write) / 1000 * 100, 100);

            const readColor = read > 300 ? '\x1b[32m' : read > 100 ? '\x1b[33m' : '\x1b[31m';
            const writeColor = write > 300 ? '\x1b[32m' : write > 100 ? '\x1b[33m' : '\x1b[31m';

            const barLen = 20;
            const filled = Math.floor(load / 100 * barLen);
            const bar = '█'.repeat(filled) + '░'.repeat(barLen - filled);

            console.log(`${disk.padEnd(6)} ${readColor}${read.toFixed(1).padStart(8)}\x1b[0m     ${writeColor}${write.toFixed(1).padStart(8)}\x1b[0m     ${bar} ${load.toFixed(0).padStart(5)}%`);
        }
    }

    printStats() {
        console.log(`\n\x1b[36m📊 Статистика:\x1b[0m`);
        for (const [disk, stat] of Object.entries(this.stats)) {
            if (stat.read.length > 0) {
                const avgRead = stat.read.reduce((a, b) => a + b, 0) / stat.read.length;
                const avgWrite = stat.write.reduce((a, b) => a + b, 0) / stat.write.length;
                console.log(`  ${disk}:`);
                console.log(`    Средняя скорость чтения: ${avgRead.toFixed(1)} МБ/с`);
                console.log(`    Средняя скорость записи: ${avgWrite.toFixed(1)} МБ/с`);
                console.log(`    Пиковая скорость чтения: ${stat.maxRead.toFixed(1)} МБ/с`);
                console.log(`    Пиковая скорость записи: ${stat.maxWrite.toFixed(1)} МБ/с`);
            }
        }
    }

    async run() {
        console.log('\x1b[36m💾 Disk Speed Monitor (JavaScript)\x1b[0m');
        console.log(`Интервал: ${this.interval} сек`);
        console.log('Нажмите Ctrl+C для остановки...');

        this.prevData = await this.getDiskIO();
        await new Promise(resolve => setTimeout(resolve, this.interval * 1000));

        const intervalId = setInterval(async () => {
            try {
                const curr = await this.getDiskIO();
                const speeds = this.calculateSpeed(this.prevData, curr, this.interval);
                this.printSpeeds(speeds);
                this.prevData = curr;
            } catch (err) {
                console.error(`\x1b[31m❌ Ошибка: ${err.message}\x1b[0m`);
            }
        }, this.interval * 1000);

        process.on('SIGINT', () => {
            clearInterval(intervalId);
            console.log('\n\n⏹️ Остановка...');
            this.printStats();
            process.exit(0);
        });
    }
}

const interval = parseInt(process.argv[2]) || 2;
const monitor = new DiskSpeedMonitor(interval);
monitor.run().catch(console.error);
