using System;
using System.Runtime.InteropServices;
using Gdk;

public class GdkX11Helper
{
    // gdk_x11_window_foreign_new_for_display (gdk_display, xid) -> GdkWindow*
    [DllImport("libgdk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr gdk_x11_window_foreign_new_for_display(IntPtr display, IntPtr window_xid);

    // gdk_x11_display_get_xdisplay (gdk_display) -> Display*
    [DllImport("libgdk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr gdk_x11_display_get_xdisplay(IntPtr gdk_display);

    public static Gdk.Window ForeignNewForDisplay(IntPtr x11WindowXid)
    {
        var display = Display.Default;
        IntPtr gdkDisplayPtr = display.Handle;
        
        IntPtr foreign = gdk_x11_window_foreign_new_for_display(gdkDisplayPtr, x11WindowXid);
        if (foreign == IntPtr.Zero)
            throw new Exception("Failed to create foreign GdkWindow");
        
        return (Gdk.Window)GLib.Object.GetObject(foreign, false);
    }
}