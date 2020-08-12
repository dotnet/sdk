using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.Configurer;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Cli.Utils
{
    internal sealed class PerformanceLogManager
    {
        private const string PerfLogRootEnvVar = "DOTNET_PERFLOG_ROOT";
        private const string PerfLogRoot = "PerformanceLogs";
        private const int NumLogsToKeep = 10;

        private IFileSystem _fileSystem;
        private string _perfLogRoot;
        private string _currentLogDir;
        private DateTime _initializationTime;

        internal static PerformanceLogManager Instance
        {
            get;
            private set;
        }

        internal static void Initialize(IFileSystem fileSystem)
        {
            if(Instance == null)
            {
                Instance = new PerformanceLogManager(fileSystem);
                Instance.CreateLogDirectory();

                Task.Factory.StartNew(() =>
                {
                    Instance.CleanupOldLogs();
                });
            }
        }

        internal PerformanceLogManager(IFileSystem fileSystem)
        {
            _initializationTime = DateTime.Now;
            _fileSystem = fileSystem;
            _perfLogRoot = Path.Combine(CliFolderPathCalculator.DotnetUserProfileFolderPath, PerfLogRoot);
        }

        internal string CurrentLogDirectory
        {
            get { return _currentLogDir; }
        }

        private void CreateLogDirectory()
        {
            // Ensure the log root directory exists.
            if(!_fileSystem.Directory.Exists(_perfLogRoot))
            {
                _fileSystem.Directory.CreateDirectory(_perfLogRoot);
            }

            // Create a new perf log directory.
            _currentLogDir = Path.Combine(_perfLogRoot, Guid.NewGuid().ToString("N"));
            _fileSystem.Directory.CreateDirectory(_currentLogDir);
        }

        private void CleanupOldLogs()
        {
            if(_fileSystem.Directory.Exists(_perfLogRoot))
            {
                List<DirectoryInfo> logDirectories = new List<DirectoryInfo>();
                foreach(string directoryPath in _fileSystem.Directory.EnumerateDirectories(_perfLogRoot))
                {
                    // TODO: Convert to abstraction.
                    logDirectories.Add(new DirectoryInfo(directoryPath));
                }

                // Sort the list.
                logDirectories.Sort(new LogDirectoryComparer());

                // Skip the first NumLogsToKeep elements.
                if(logDirectories.Count > NumLogsToKeep)
                {
                    // Prune the old logs.
                    for(int i = logDirectories.Count - NumLogsToKeep - 1; i>=0; i--)
                    {
                        try
                        {
                            logDirectories[i].Delete(true);
                        }
                        catch
                        {
                            // TODO: Log.
                        }
                    }
                }
            }
        }

        internal void AddLogRoot(ProcessStartInfo startInfo)
        {
            Debug.Assert(_currentLogDir != null);

            if (_perfLogRoot != null)
            {
                startInfo.EnvironmentVariables.Add(PerfLogRootEnvVar, _currentLogDir);
            }
        }
    }

    internal sealed class LogDirectoryComparer : IComparer<DirectoryInfo>
    {
        int IComparer<DirectoryInfo>.Compare(DirectoryInfo x, DirectoryInfo y)
        {
            return x.CreationTime.CompareTo(y.CreationTime);
        }
    }
}
