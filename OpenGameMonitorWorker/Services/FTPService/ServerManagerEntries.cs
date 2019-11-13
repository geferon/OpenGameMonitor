using FubarDev.FtpServer.FileSystem;
using FubarDev.FtpServer.FileSystem.Generic;
using OpenGameMonitorLibraries;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OpenGameMonitorWorker
{
    class ServerManagerFileSystemEntry : IUnixFileSystemEntry
    {
        protected ServerManagerFileSystemEntry(Server server)
        {
            Server = server;
        }

        protected ServerManagerFileSystemEntry(Server server, FileSystemInfo fsInfo)
        {
            Server = server;
            Info = fsInfo;
            LastWriteTime = new DateTimeOffset(Info.LastWriteTime);
            CreatedTime = new DateTimeOffset(Info.CreationTimeUtc);
            var accessMode = new GenericAccessMode(true, true, true);
            Permissions = new GenericUnixPermissions(accessMode, accessMode, accessMode);
        }

        public Server? Server { get; set; }

        public FileSystemInfo? Info { get; set; }

        public string Name => Info?.Name ?? "";

        public IUnixPermissions Permissions { get; set; }

        public DateTimeOffset? LastWriteTime { get; set; }

        public DateTimeOffset? CreatedTime { get; set; }

        public long NumberOfLinks => 1;

        public string Owner => "owner";

        public string Group => "owner";
    }

    class ServerManagerDirectoryEntry : ServerManagerFileSystemEntry, IUnixDirectoryEntry
    {
        public bool IsRoot { get; }

        public bool IsDeletable => CheckIfDeletable();

        public string Name => throw new NotImplementedException();

        public IUnixPermissions Permissions => throw new NotImplementedException();

        public DateTimeOffset? LastWriteTime => throw new NotImplementedException();

        public DateTimeOffset? CreatedTime => throw new NotImplementedException();

        public long NumberOfLinks => throw new NotImplementedException();

        public string Owner => throw new NotImplementedException();

        public string Group => throw new NotImplementedException();
    }
}
