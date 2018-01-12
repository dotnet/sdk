using System.Diagnostics;

namespace BaselineComparer.Helpers
{
    // copied from ProjectTestRunner
    public class Proc
    {
        public static ProcessEx Run(string command, string args)
        {
            ProcessStartInfo psi = new ProcessStartInfo(command, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process p = Process.Start(psi);
            ProcessEx wrapper = new ProcessEx(p);
            return wrapper;
        }
    }
}
