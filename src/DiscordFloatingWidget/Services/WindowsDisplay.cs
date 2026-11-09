using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DiscordFloatingWidget.Services;

internal static class WindowsDisplay
{
    public static bool IsNormal(nint window) => !IsIconic(window) && !IsZoomed(window);
    public static PixelBounds GetBounds(nint window)
    {
        if (!GetWindowRect(window, out var bounds)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return bounds.ToBounds();
    }

    public static PixelBounds WorkArea(PixelBounds bounds)
    {
        var rectangle = new NativeRect(bounds);
        var monitor = MonitorFromRect(ref rectangle, 2); // MONITOR_DEFAULTTONEAREST
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return info.Work.ToBounds();
    }

    public static double Scale(nint window)
    {
        var dpi = GetDpiForWindow(window);
        if (dpi == 0) throw new Win32Exception("Unable to read the window's display scaling.");
        return dpi / 96d;
    }

    public static void Move(nint window, PixelBounds bounds)
    {
        // Do not activate the window or change its WPF Topmost z-order.
        if (!SetWindowPos(window, 0, bounds.Left, bounds.Top, bounds.Width, bounds.Height, 0x0014))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
        public NativeRect(PixelBounds bounds)
        {
            Left = bounds.Left; Top = bounds.Top;
            Right = bounds.Left + bounds.Width; Bottom = bounds.Top + bounds.Height;
        }
        public readonly PixelBounds ToBounds() => new(Left, Top, Right - Left, Bottom - Top);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor, Work;
        public uint Flags;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out NativeRect bounds);
    [DllImport("user32.dll")]
    private static extern nint MonitorFromRect(ref NativeRect bounds, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint window);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsZoomed(nint window);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);
}
