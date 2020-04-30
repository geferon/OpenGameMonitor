using FubarDev.FtpServer.BackgroundTransfer;
using FubarDev.FtpServer.FileSystem;
using FubarDev.FtpServer.FileSystem.DotNet;
using OpenGameMonitorLibraries;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using FubarDev.FtpServer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace OpenGameMonitorWorker.Services
{
    class ServerManagerFileSystem : IUnixFileSystem
    {
        /// <summary>
        /// The default buffer size for copying from one stream to another.
        /// </summary>
        public static readonly int DefaultStreamBufferSize = 4096;

        private readonly int _streamBufferSize;
        private readonly bool _flushStream;

        private readonly IServiceProvider _serviceProvider;
        private readonly UserManager<MonitorUser> _userManager;
        private IAccountInformation _account;

        /// <summary>
        /// Initializes a new instance of the <see cref="DotNetFileSystem"/> class.
        /// </summary>
        /// <param name="rootPath">The path to use as root.</param>
        /// <param name="allowNonEmptyDirectoryDelete">Defines whether the deletion of non-empty directories is allowed.</param>
        public ServerManagerFileSystem(IAccountInformation account, IServiceProvider serviceProvider, string rootPath, bool allowNonEmptyDirectoryDelete)
            : this(account, serviceProvider, rootPath, allowNonEmptyDirectoryDelete, DefaultStreamBufferSize)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DotNetFileSystem"/> class.
        /// </summary>
        /// <param name="rootPath">The path to use as root.</param>
        /// <param name="allowNonEmptyDirectoryDelete">Defines whether the deletion of non-empty directories is allowed.</param>
        /// <param name="streamBufferSize">Buffer size to be used in async IO methods.</param>
        public ServerManagerFileSystem(IAccountInformation account, IServiceProvider serviceProvider, string rootPath, bool allowNonEmptyDirectoryDelete, int streamBufferSize)
            : this(account, serviceProvider, rootPath, allowNonEmptyDirectoryDelete, streamBufferSize, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DotNetFileSystem"/> class.
        /// </summary>
        /// <param name="rootPath">The path to use as root.</param>
        /// <param name="allowNonEmptyDirectoryDelete">Defines whether the deletion of non-empty directories is allowed.</param>
        /// <param name="streamBufferSize">Buffer size to be used in async IO methods.</param>
        /// <param name="flushStream">Flush the stream after every write operation.</param>
        public ServerManagerFileSystem(IAccountInformation account, IServiceProvider serviceProvider, string rootPath, bool allowNonEmptyDirectoryDelete, int streamBufferSize, bool flushStream)
        {
            _account = account;
            _serviceProvider = serviceProvider;
            //_userManager = _serviceProvider.GetService<UserManager<MonitorUser>>();

            FileSystemEntryComparer = StringComparer.OrdinalIgnoreCase;
            Root = new ServerManagerDirectoryEntry(allowNonEmptyDirectoryDelete);
            SupportsNonEmptyDirectoryDelete = allowNonEmptyDirectoryDelete;
            _streamBufferSize = streamBufferSize;
            _flushStream = flushStream;
        }

        /// <inheritdoc/>
        public bool SupportsNonEmptyDirectoryDelete { get; }

        /// <inheritdoc/>
        public StringComparer FileSystemEntryComparer { get; }

        /// <inheritdoc/>
        public IUnixDirectoryEntry Root { get; }

        /// <inheritdoc/>
        public bool SupportsAppend => true;

        /// <inheritdoc/>
        public async Task<IReadOnlyList<IUnixFileSystemEntry>> GetEntriesAsync(IUnixDirectoryEntry directoryEntry, CancellationToken cancellationToken)
        {
            var result = new List<IUnixFileSystemEntry>();
            var managerDirEntry = ((ServerManagerDirectoryEntry)directoryEntry);
            if (managerDirEntry.IsRoot)
            {
                List<Server> servers;
                using (var db = _serviceProvider.GetService<MonitorDBContext>())
                {
                    IQueryable<Server> serversQuery = db.Servers;
                    var User = _account.FtpUser;
                    var user = await _userManager.GetUserAsync(User);

                    if (!User.IsInRole("Admin"))
                    {
                        serversQuery = serversQuery.Where((server) =>
                            user == server.Owner ||
                            (server.Group != null
                            ? server.Group.Members.Any((group) => group.User == user)
                            : false)
                        );
                    }

                    servers = await serversQuery.ToListAsync();
                }

                foreach (var server in servers)
                {
                    result.Add(new ServerManagerDirectoryEntry(server, new DirectoryInfo(server.Path), SupportsNonEmptyDirectoryDelete));
                }
            }
            else if (managerDirEntry.Info != null)
            {
                var searchDirInfo = (DirectoryInfo) managerDirEntry.Info;
                foreach (var info in searchDirInfo.EnumerateFileSystemInfos())
                {
                    if (info is DirectoryInfo dirInfo)
                    {
                        result.Add(new ServerManagerDirectoryEntry(managerDirEntry.Server, dirInfo, SupportsNonEmptyDirectoryDelete));
                    }
                    else
                    {
                        if (info is FileInfo fileInfo)
                        {
                            result.Add(new ServerManagerFileEntry(managerDirEntry.Server, fileInfo));
                        }
                    }
                }
            }

            return result;
        }

        /// <inheritdoc/>
        public Task<IUnixFileSystemEntry?> GetEntryByNameAsync(IUnixDirectoryEntry directoryEntry, string name, CancellationToken cancellationToken)
        {
            var managerDirEntry = (ServerManagerDirectoryEntry)directoryEntry;
            IUnixFileSystemEntry? result = null;

            if (managerDirEntry.IsRoot || managerDirEntry.Info == null)
            {
                result = null;
            }
            else
            {
                var searchDirInfo = managerDirEntry.Info;
                var fullPath = Path.Combine(searchDirInfo.FullName, name);
                if (File.Exists(fullPath))
                {
                    result = new ServerManagerFileEntry(managerDirEntry.Server, new FileInfo(fullPath));
                }
                else if (Directory.Exists(fullPath))
                {
                    result = new ServerManagerDirectoryEntry(managerDirEntry.Server, new DirectoryInfo(fullPath), SupportsNonEmptyDirectoryDelete);
                }
            }

            return Task.FromResult(result);
        }

        /// <inheritdoc/>
        public Task<IUnixFileSystemEntry> MoveAsync(IUnixDirectoryEntry parent, IUnixFileSystemEntry source, IUnixDirectoryEntry target, string fileName, CancellationToken cancellationToken)
        {
            var targetEntry = (ServerManagerDirectoryEntry)target;

            if (targetEntry.Info == null)
            {
                throw new InvalidOperationException();
            }

            var targetName = Path.Combine(targetEntry.Info.FullName, fileName);

            if (source is ServerManagerFileEntry sourceFileEntry)
            {
                ((FileInfo)sourceFileEntry.Info).MoveTo(targetName);
                return Task.FromResult<IUnixFileSystemEntry>(new ServerManagerFileEntry(targetEntry.Server, new FileInfo(targetName)));
            }

            var sourceDirEntry = (ServerManagerDirectoryEntry)source;
            ((DirectoryInfo)sourceDirEntry.Info).MoveTo(targetName);
            return Task.FromResult<IUnixFileSystemEntry>(new ServerManagerDirectoryEntry(targetEntry.Server, new DirectoryInfo(targetName), SupportsNonEmptyDirectoryDelete));
        }

        /// <inheritdoc/>
        public Task UnlinkAsync(IUnixFileSystemEntry entry, CancellationToken cancellationToken)
        {
            if (entry is ServerManagerDirectoryEntry dirEntry)
            {
                if (dirEntry.Info == null)
                {
                    throw new InvalidOperationException();
                }
                ((DirectoryInfo)dirEntry.Info).Delete(SupportsNonEmptyDirectoryDelete);
            }
            else
            {
                var fileEntry = (ServerManagerFileEntry)entry;
                fileEntry.Info.Delete();
            }

            return Task.FromResult(0);
        }

        /// <inheritdoc/>
        public Task<IUnixDirectoryEntry> CreateDirectoryAsync(IUnixDirectoryEntry targetDirectory, string directoryName, CancellationToken cancellationToken)
        {
            var targetEntry = (ServerManagerDirectoryEntry)targetDirectory;

            // Do not allow directory creation on root
            if (targetEntry.Info == null || targetEntry.IsRoot)
            {
                throw new InvalidOperationException();
            }

            var newDirInfo = ((DirectoryInfo)targetEntry.Info).CreateSubdirectory(directoryName);
            return Task.FromResult<IUnixDirectoryEntry>(new ServerManagerDirectoryEntry(targetEntry.Server, newDirInfo, SupportsNonEmptyDirectoryDelete));
        }

        /// <inheritdoc/>
        public Task<Stream> OpenReadAsync(IUnixFileEntry fileEntry, long startPosition, CancellationToken cancellationToken)
        {
            var fileInfo = (FileInfo)((ServerManagerFileEntry)fileEntry).Info;
            var input = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (startPosition != 0)
            {
                input.Seek(startPosition, SeekOrigin.Begin);
            }

            return Task.FromResult<Stream>(input);
        }

        /// <inheritdoc/>
        public async Task<IBackgroundTransfer?> AppendAsync(IUnixFileEntry fileEntry, long? startPosition, Stream data, CancellationToken cancellationToken)
        {
            var fileInfo = (FileInfo)((ServerManagerFileEntry)fileEntry).Info;
            using (var output = fileInfo.OpenWrite())
            {
                if (startPosition == null)
                {
                    startPosition = fileInfo.Length;
                }

                output.Seek(startPosition.Value, SeekOrigin.Begin);
                await data.CopyToAsync(output, _streamBufferSize, _flushStream, cancellationToken);
            }

            return null;
        }

        /// <inheritdoc/>
        public async Task<IBackgroundTransfer?> CreateAsync(IUnixDirectoryEntry targetDirectory, string fileName, Stream data, CancellationToken cancellationToken)
        {
            var targetEntry = (ServerManagerDirectoryEntry)targetDirectory;

            // Do not allow directory creation on root
            if (targetEntry.Info == null || targetEntry.IsRoot)
            {
                throw new InvalidOperationException();
            }

            var fileInfo = new FileInfo(Path.Combine(targetEntry.Info.FullName, fileName));
            using (var output = fileInfo.Create())
            {
                await data.CopyToAsync(output, _streamBufferSize, _flushStream, cancellationToken);
            }

            return null;
        }

        /// <inheritdoc/>
        public async Task<IBackgroundTransfer?> ReplaceAsync(IUnixFileEntry fileEntry, Stream data, CancellationToken cancellationToken)
        {
            var fileInfo = (FileInfo)((ServerManagerFileEntry)fileEntry).Info;
            using (var output = fileInfo.OpenWrite())
            {
                await data.CopyToAsync(output, _streamBufferSize, _flushStream, cancellationToken);
                output.SetLength(output.Position);
            }

            return null;
        }

        /// <summary>
        /// Sets the modify/access/create timestamp of a file system item.
        /// </summary>
        /// <param name="entry">The <see cref="IUnixFileSystemEntry"/> to change the timestamp for.</param>
        /// <param name="modify">The modification timestamp.</param>
        /// <param name="access">The access timestamp.</param>
        /// <param name="create">The creation timestamp.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The modified <see cref="IUnixFileSystemEntry"/>.</returns>
        public Task<IUnixFileSystemEntry> SetMacTimeAsync(IUnixFileSystemEntry entry, DateTimeOffset? modify, DateTimeOffset? access, DateTimeOffset? create, CancellationToken cancellationToken)
        {
            var item = ((ServerManagerFileSystemEntry)entry).Info;

            if (item == null)
            {
                throw new InvalidOperationException();
            }

            if (access != null)
            {
                item.LastAccessTimeUtc = access.Value.UtcDateTime;
            }

            if (modify != null)
            {
                item.LastWriteTimeUtc = modify.Value.UtcDateTime;
            }

            if (create != null)
            {
                item.CreationTimeUtc = create.Value.UtcDateTime;
            }

            if (entry is ServerManagerDirectoryEntry dirEntry)
            {
                return Task.FromResult<IUnixFileSystemEntry>(new ServerManagerDirectoryEntry((DirectoryInfo)item, SupportsNonEmptyDirectoryDelete));
            }

            return Task.FromResult<IUnixFileSystemEntry>(new DotNetFileEntry((FileInfo)item));
        }


    }
}
