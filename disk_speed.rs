// disk_speed.rs — Rust версия

use sysinfo::{System, SystemExt, DiskExt};
use std::{thread, time};
use std::collections::HashMap;

struct DiskStats {
    read_speeds: Vec<f64>,
    write_speeds: Vec<f64>,
    max_read: f64,
    max_write: f64,
}

impl DiskStats {
    fn new() -> Self {
        DiskStats {
            read_speeds: Vec::new(),
            write_speeds: Vec::new(),
            max_read: 0.0,
            max_write: 0.0,
        }
    }
}

fn main() {
    let interval = 2;
    let mut stats: HashMap<String, DiskStats> = HashMap::new();
    let mut prev_read: HashMap<String, u64> = HashMap::new();
    let mut prev_write: HashMap<String, u64> = HashMap::new();

    println!("\x1b[36m💾 Disk Speed Monitor (Rust)\x1b[0m");
    println!("Интервал: {} сек", interval);
    println!("Нажмите Ctrl+C для остановки...");

    let mut sys = System::new_all();
    sys.refresh_all();

    for disk in sys.disks() {
        let name = disk.name().to_str().unwrap_or("unknown").to_string();
        prev_read.insert(name.clone(), disk.total_read_bytes());
        prev_write.insert(name, disk.total_written_bytes());
    }

    thread::sleep(time::Duration::from_secs(interval));

    loop {
        sys.refresh_all();

        let mut curr_read: HashMap<String, u64> = HashMap::new();
        let mut curr_write: HashMap<String, u64> = HashMap::new();

        for disk in sys.disks() {
            let name = disk.name().to_str().unwrap_or("unknown").to_string();
            curr_read.insert(name.clone(), disk.total_read_bytes());
            curr_write.insert(name, disk.total_written_bytes());
        }

        let mut speeds: HashMap<String, (f64, f64)> = HashMap::new();

        for (disk, read) in &curr_read {
            if let Some(prev_r) = prev_read.get(disk) {
                let read_mb = (*read - *prev_r) as f64 / (1024.0 * 1024.0);
                let write_mb = (*curr_write.get(disk).unwrap() - *prev_write.get(disk).unwrap()) as f64 / (1024.0 * 1024.0);
                let read_speed = read_mb / interval as f64;
                let write_speed = write_mb / interval as f64;
                speeds.insert(disk.clone(), (read_speed, write_speed));

                if !stats.contains_key(disk) {
                    stats.insert(disk.clone(), DiskStats::new());
                }
                let s = stats.get_mut(disk).unwrap();
                s.read_speeds.push(read_speed);
                s.write_speeds.push(write_speed);
                if read_speed > s.max_read { s.max_read = read_speed; }
                if write_speed > s.max_write { s.max_write = write_speed; }
            }
        }

        // Вывод
        println!("\n{}", "─".repeat(60));
        println!("\x1b[36mДиск   Чтение (МБ/с)   Запись (МБ/с)   Загрузка\x1b[0m");
        println!("{}", "─".repeat(60));

        for (disk, (read, write)) in &speeds {
            let load = ((read + write) / 1000.0 * 100.0).min(100.0);

            let read_color = if read > 300.0 { "\x1b[32m" } else if read > 100.0 { "\x1b[33m" } else { "\x1b[31m" };
            let write_color = if write > 300.0 { "\x1b[32m" } else if write > 100.0 { "\x1b[33m" } else { "\x1b[31m" };

            let bar_len = 20;
            let filled = (load / 100.0 * bar_len as f64) as usize;
            let bar = format!("{}{}", "█".repeat(filled), "░".repeat(bar_len - filled));

            println!("{:<6} {}{:>8.1}\x1b[0m     {}{:>8.1}\x1b[0m     {} {:>5.0}%",
                disk, read_color, read, write_color, write, bar, load);
        }

        prev_read = curr_read;
        prev_write = curr_write;

        thread::sleep(time::Duration::from_secs(interval));
    }
}
