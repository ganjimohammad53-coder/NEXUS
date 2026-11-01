using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Nexus.App.Services;

/// <summary>Captures the visible R6 game window; does not inject into or inspect game memory.</summary>
public static class R6LiveCaptureService
{
    private static readonly string[] ProcessNames = { "RainbowSix", "RainbowSix_Vulkan", "RainbowSix_BE" };

    public static string CaptureGameWindowJpegBase64()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("R6 live window capture currently requires Windows.");
        Process? game = null;
        foreach (var name in ProcessNames)
        {
            game = Process.GetProcessesByName(name).FirstOrDefault(p => SafeMainWindow(p) != IntPtr.Zero);
            if (game != null) break;
        }
        if (game == null) throw new InvalidOperationException("Rainbow Six Siege game window not found. Launch the game, wait for its window to appear, then try again.");
        try
        {
            var handle = SafeMainWindow(game);
            if (handle == IntPtr.Zero || !GetWindowRectNative(handle, out var rect)) throw new InvalidOperationException("Could not locate the R6 game window.");
            if (GetForegroundWindow() != handle) throw new InvalidOperationException("Bring Rainbow Six Siege to the foreground. NEXUS captures only while the R6 game window is active, so other windows are not sent to Ollama.");
            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;
            if (width < 320 || height < 200 || width > 10000 || height > 10000) throw new InvalidOperationException("The R6 window size is invalid or minimized. Restore the game window and retry.");
            using var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            using (var graphics = Graphics.FromImage(bitmap)) graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
            using var output = new MemoryStream();
            var jpeg = ImageCodecInfo.GetImageEncoders().First(e => e.MimeType == "image/jpeg");
            using var parameters = new EncoderParameters(1);
            parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 75L);
            bitmap.Save(output, jpeg, parameters);
            if (output.Length > 8 * 1024 * 1024) throw new InvalidOperationException("Captured frame is over 8 MB. Lower the game's resolution and retry.");
            return Convert.ToBase64String(output.ToArray());
        }
        finally { game.Dispose(); }
    }

    private static IntPtr SafeMainWindow(Process process)
    {
        try { return process.HasExited ? IntPtr.Zero : process.MainWindowHandle; }
        catch { return IntPtr.Zero; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    [DllImport("user32.dll", EntryPoint = "GetWindowRect", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRectNative(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
}
