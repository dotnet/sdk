using System;
using System.Windows;
 
class C
{
    [STAThread]
    static void Main(string[] args)
    {
        var app = new Application();
        var window = new Window();
        app.Run(window);
    }
}