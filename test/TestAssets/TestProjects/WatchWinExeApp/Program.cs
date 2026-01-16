// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Windows.Forms;

// <metadata update handler placeholder>

namespace WatchWinExeApp;

static class Program
{
    [STAThread]
    static int Main()
    {
        ApplicationConfiguration.Initialize();
        var form = new MainForm();
        Application.Run(form);

        // Return exit code based on how the form was closed
        return form.ClosedGracefully ? 0 : 1;
    }
}

class MainForm : Form
{
    public bool ClosedGracefully { get; private set; }

    public MainForm()
    {
        Text = "WatchWinExeApp";
        FormClosing += OnFormClosing;
        Shown += OnShown;
    }

    private void OnShown(object sender, EventArgs e)
    {
        // Print PID so the test can identify us
        Console.WriteLine(Process.GetCurrentProcess().Id);
        Console.WriteLine("Started");
    }

    private void OnFormClosing(object sender, FormClosingEventArgs e)
    {
        // Mark that we were closed gracefully (via CloseMainWindow)
        ClosedGracefully = e.CloseReason == CloseReason.UserClosing || 
                          e.CloseReason == CloseReason.WindowsShutDown ||
                          e.CloseReason == CloseReason.TaskManagerClosing;
        
        Console.WriteLine($"Closing gracefully: {ClosedGracefully} (reason: {e.CloseReason})");
    }
}
