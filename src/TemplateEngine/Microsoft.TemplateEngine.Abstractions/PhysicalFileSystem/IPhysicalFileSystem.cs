using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem
{
    public interface IPhysicalFileSystem
    {
        bool DirectoryExists(string directory);

        bool FileExists(string file);

        Stream CreateFile(string path);

        void CreateDirectory(string path);

        string GetCurrentDirectory();

        IEnumerable<string> EnumerateFileSystemEntries(string directoryName, string pattern, SearchOption searchOption);

        void FileCopy(string sourcePath, string targetPath, bool overwrite);

        void DirectoryDelete(string path, bool recursive);

        string ReadAllText(string path);

        void WriteAllText(string path, string value);

        IEnumerable<string> EnumerateDirectories(string path, string pattern, SearchOption searchOption);

        IEnumerable<string> EnumerateFiles(string path, string pattern, SearchOption searchOption);

        Stream OpenRead(string path);

        void FileDelete(string path);

        FileAttributes GetFileAttributes(string file);

        void SetFileAttributes(string file, FileAttributes attributes);

        IDisposable WatchFileChanges(string filepath, FileSystemEventHandler fileChanged);
    }
}
