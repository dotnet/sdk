using System;
using System.Windows;
using System.Windows.Interop;

namespace dotnet_new3.VSIX
{
    public static class WindowExtensions
    {
        public static void CenterInVs(this Window window)
        {
            IntPtr hwnd = new IntPtr(PrimaryPackage.DTE.MainWindow.HWnd);
            Window vs = HwndSource.FromHwnd(hwnd)?.RootVisual as Window;
            window.Owner = vs;
        }
    }
}
