using System;
using System.Windows;
using System.Windows.Interop;

namespace dotnet_new3.VSIX
{
    public static class WindowExtensions
    {
        public static void CenterInVs(this Window window)
        {
            var hwnd = new IntPtr(PrimaryPackage.DTE.MainWindow.HWnd);
            var vs = (Window)HwndSource.FromHwnd(hwnd).RootVisual;
            window.Owner = vs;
        }
    }
}
