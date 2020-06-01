using FubarDev.FtpServer.FileSystem;
using FubarDev.FtpServer.FileSystem.Generic;
using OpenGameMonitorLibraries;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenGameMonitorWorker.Services
{
    class ServerManagerFileSystemEntry : IUnixFileSystemEntry
    {
        public ServerManagerFileSystemEntry(FileSystemInfo fsInfo)
        {
            Info = fsInfo;
            LastWriteTime = new DateTimeOffset(Info.LastWriteTime);
            CreatedTime = new DateTimeOffset(Info.CreationTimeUtc);

            var accessMode = new GenericAccessMode(true, true, true);
            Permissions = new GenericUnixPermissions(accessMode, accessMode, accessMode);
        }

        // All servers have a path, this is pointless?
        /*
        public ServerManagerFileSystemEntry(Server server)
        {
            Server = server;
            CreatedTime = server.Created;
            LastWriteTime = server.LastModified;

            var accessMode = new GenericAccessMode(true, true, true);
            Permissions = new GenericUnixPermissions(accessMode, accessMode, accessMode);
        }
        */

        public ServerManagerFileSystemEntry(Server server, FileSystemInfo fsInfo)
            : this(fsInfo)
        {
            Server = server;
            CreatedTime = server.Inserted;
            LastWriteTime = server.Updated;

            //var accessMode = new GenericAccessMode(true, true, true);
            //Permissions = new GenericUnixPermissions(accessMode, accessMode, accessMode);
        }

        public Server? Server { get; set; }

        public FileSystemInfo? Info { get; set; }

        public string Name => Info?.Name ?? Server?.Name ?? "";

        public IUnixPermissions Permissions { get; set; }

        public DateTimeOffset? LastWriteTime { get; set; }

        public DateTimeOffset? CreatedTime { get; set; }

        public long NumberOfLinks => 1;

        public string Owner => "owner";

        public string Group => "owner";
    }

    class ServerManagerDirectoryEntry : ServerManagerFileSystemEntry, IUnixDirectoryEntry
    {
        private readonly bool _allowDeleteIfNotEmpty;

        public ServerManagerDirectoryEntry(DirectoryInfo dirInfo, bool allowDeleteIfNotEmpty) : base(dirInfo)
        {
            IsRoot = true;
            _allowDeleteIfNotEmpty = allowDeleteIfNotEmpty;
        }

        // All servers have a path, this is pointless?
        /*
        public ServerManagerDirectoryEntry(Server server, bool allowDeleteIfNotEmpty) : base(server)
        {
            IsRoot = false;
            _allowDeleteIfNotEmpty = allowDeleteIfNotEmpty;
        }
        */

        public ServerManagerDirectoryEntry(Server server, DirectoryInfo dirInfo, bool allowDeleteIfNotEmpty, bool isSubRoot = false) : base(server, dirInfo)
        {
            IsRoot = false;
            IsSubRoot = isSubRoot;
            _allowDeleteIfNotEmpty = allowDeleteIfNotEmpty;
        }

        public bool IsRoot { get; }
        public bool IsSubRoot { get; }

        public bool IsDeletable => CheckIfDeletable();

        public new string Name => IsSubRoot ? Server?.Name : Info?.Name ?? Server?.Name ?? "";

        private static bool? HasChildEntries(DirectoryInfo directoryInfo)
        {
            try
            {
                return directoryInfo.EnumerateFileSystemInfos().Any();
            }
            catch
            {
                return null;
            }
        }

        private bool CheckIfDeletable()
        {
            if (IsRoot)
            {
                return false;
            }

            // A server can't be deleted
            if (Server != null && Info == null)
            {
                return false;
            }

            var hasChildrenEntries = HasChildEntries((DirectoryInfo)Info);
            if (hasChildrenEntries == null)
            {
                return false;
            }

            return _allowDeleteIfNotEmpty || !hasChildrenEntries.Value;
        }
    }

    class ServerManagerFileEntry : ServerManagerFileSystemEntry, IUnixFileEntry
    {
        public ServerManagerFileEntry(Server server, FileInfo info) : base(server, info)
        {

        }

        public long Size => ((FileInfo)Info).Length;
    }
}
