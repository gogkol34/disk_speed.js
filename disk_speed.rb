# disk_speed.rb — Ruby версия

require 'sys/filesystem'
require 'sys/proctable'
require 'json'
require 'time'
require 'io/console'

class DiskSpeedMonitor
  def initialize(interval = 2)
    @interval = interval
    @stats = {}
    @prev_read = {}
    @prev_write = {}
  end

  def get_disk_io
    read = {}
    write = {}

    if RUBY_PLATFORM =~ /linux/
      # Linux: читаем /proc/diskstats
      File.readlines('/proc/diskstats').each do |line|
        parts = line.strip.split
        next if parts.length < 14
        # parts[2] - имя диска, parts[5] - read_sectors, parts[9] - write_sectors
        disk = parts[2]
        read_sectors = parts[5].to_i * 512
        write_sectors = parts[9].to_i * 512
        read[disk] = read_sectors
        write[disk] = write_sectors
      end
    elsif RUBY_PLATFORM =~ /darwin/
      # macOS: используем iostat
      output = `iostat -d 1 2 2>/dev/null`
      output.each_line do |line|
        parts = line.strip.split
        next if parts.length < 4 || parts[0] == 'disk'
        disk = parts[0]
        read[disk] = (parts[3].to_f * 1024).to_i
        write[disk] = (parts[4].to_f * 1024).to_i
      end
    else
      # Windows: заглушка
      drives = Sys::Filesystem.mounts.map(&:name)
      drives.each do |drive|
        read[drive] = 0
        write[drive] = 0
      end
    end

    [read, write]
  end

  def run
    puts "\e[36m💾 Disk Speed Monitor (Ruby)\e[0m"
    puts "Интервал: #{@interval} сек"
    puts "Нажмите Ctrl+C для остановки..."

    @prev_read, @prev_write = get_disk_io
    sleep @interval

    trap('INT') do
      puts "\n\n⏹️ Остановка..."
      print_stats
      exit 0
    end

    loop do
      curr_read, curr_write = get_disk_io
      speeds = calculate_speeds(@prev_read, @prev_write, curr_read, curr_write)
      print_speeds(speeds)
      @prev_read = curr_read
      @prev_write = curr_write
      sleep @interval
    end
  end

  def calculate_speeds(prev_r, prev_w, curr_r, curr_w)
    speeds = {}
    curr_r.each do |disk, read|
      if prev_r[disk] && curr_r[disk]
        read_mb = (read - prev_r[disk]) / (1024.0 * 1024.0)
        write_mb = (curr_w[disk] - prev_w[disk]) / (1024.0 * 1024.0)
        read_speed = read_mb / @interval
        write_speed = write_mb / @interval

        speeds[disk] = { read: read_speed, write: write_speed }

        @stats[disk] ||= { read: [], write: [], max_read: 0, max_write: 0 }
        @stats[disk][:read] << read_speed
        @stats[disk][:write] << write_speed
        @stats[disk][:max_read] = read_speed if read_speed > @stats[disk][:max_read]
        @stats[disk][:max_write] = write_speed if write_speed > @stats[disk][:max_write]
      end
    end
    speeds
  end

  def print_speeds(speeds)
    puts "\n" + "─" * 60
    puts "\e[36mДиск   Чтение (МБ/с)   Запись (МБ/с)   Загрузка\e[0m"
    puts "─" * 60

    speeds.each do |disk, speed|
      read = speed[:read] || 0
      write = speed[:write] || 0
      load = [(read + write) / 1000 * 100, 100].min

      read_color = read > 300 ? "\e[32m" : read > 100 ? "\e[33m" : "\e[31m"
      write_color = write > 300 ? "\e[32m" : write > 100 ? "\e[33m" : "\e[31m"

      bar_len = 20
      filled = (load / 100 * bar_len).to_i
      bar = "█" * filled + "░" * (bar_len - filled)

      puts "#{disk.to_s.ljust(6)} #{read_color}#{read.round(1).to_s.rjust(8)}\e[0m     #{write_color}#{write.round(1).to_s.rjust(8)}\e[0m     #{bar} #{load.round.to_s.rjust(5)}%"
    end
  end

  def print_stats
    puts "\n\e[36m📊 Статистика:\e[0m"
    @stats.each do |disk, stat|
      if stat[:read].any?
        avg_read = stat[:read].sum / stat[:read].size
        avg_write = stat[:write].sum / stat[:write].size
        puts "  #{disk}:"
        puts "    Средняя скорость чтения: #{avg_read.round(1)} МБ/с"
        puts "    Средняя скорость записи: #{avg_write.round(1)} МБ/с"
        puts "    Пиковая скорость чтения: #{stat[:max_read].round(1)} МБ/с"
        puts "    Пиковая скорость записи: #{stat[:max_write].round(1)} МБ/с"
      end
    end
  end
end

interval = (ARGV[0] || 2).to_i
monitor = DiskSpeedMonitor.new(interval)
monitor.run
