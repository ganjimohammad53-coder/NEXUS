using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Nexus.App.Services;

public sealed class GameMonitorService
{
    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint Low;
        public uint High;

        public readonly ulong Value =>
            ((ulong)High << 32) | Low;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(
        out FILETIME idleTime,
        out FILETIME kernelTime,
        out FILETIME userTime);

    public sealed record GameMonitorSnapshot(
        double CpuPercent,
        string? TopProcess,
        string Status,
        DateTime Timestamp,
        string Recommendation);

    public async Task<GameMonitorSnapshot> CaptureAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "System CPU telemetry is currently supported on Windows only.");
        }

        if (!GetSystemTimes(out var idle1, out var kernel1, out var user1))
        {
            throw new InvalidOperationException(
                $"Windows CPU telemetry failed (error {Marshal.GetLastWin32Error()}).");
        }

        await Task.Delay(400).ConfigureAwait(false);

        if (!GetSystemTimes(out var idle2, out var kernel2, out var user2))
        {
            throw new InvalidOperationException(
                $"Windows CPU telemetry failed (error {Marshal.GetLastWin32Error()}).");
        }

        ulong idleDelta = idle2.Value - idle1.Value;
        ulong kernelDelta = kernel2.Value - kernel1.Value;
        ulong userDelta = user2.Value - user1.Value;
        ulong totalDelta = kernelDelta + userDelta;

        if (totalDelta == 0)
        {
            throw new InvalidOperationException(
                "CPU telemetry did not return a measurable time interval.");
        }

        double cpuPercent = Math.Clamp(
            100.0 * (totalDelta - Math.Min(idleDelta, totalDelta)) / totalDelta,
            0.0,
            100.0);

        string? topProcess = null;
        Process[] processes = Process.GetProcesses();

        try
        {
            var largest = processes
                .Select(p =>
                {
                    try
                    {
                        return new
                        {
                            Name = p.ProcessName,
                            Memory = p.WorkingSet64
                        };
                    }
                    catch
                    {
                        return null;
                    }
                })
                .Where(p => p != null)
                .OrderByDescending(p => p!.Memory)
                .FirstOrDefault();

            topProcess = largest?.Name;
        }
        finally
        {
            foreach (var process in processes)
                process.Dispose();
        }

        string status = cpuPercent >= 90
            ? "High CPU load"
            : cpuPercent >= 70
                ? "Elevated CPU load"
                : "System load normal";

        string recommendation = cpuPercent >= 90
            ? "Check resource-heavy processes before changing settings."
            : cpuPercent >= 70
                ? "Review background processes if performance is affected."
                : "No CPU optimization action indicated.";

        return new GameMonitorSnapshot(
            cpuPercent,
            topProcess,
            status,
            DateTime.Now,
            recommendation);
    }
}