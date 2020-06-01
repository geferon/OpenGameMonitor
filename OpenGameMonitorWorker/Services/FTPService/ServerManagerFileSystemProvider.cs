using FubarDev.FtpServer;
using FubarDev.FtpServer.FileSystem;
using FubarDev.FtpServer.FileSystem.DotNet;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace OpenGameMonitorWorker.Services
{
    class ServerManagerFileSystemProvider : IFileSystemClassFactory
    {
        private readonly IAccountDirectoryQuery _accountDirectoryQuery;
        private readonly ILogger<ServerManagerFileSystemProvider>? _logger;
        private readonly DotNetFileSystemOptions _options;
        private readonly IServiceProvider _serviceProvider;
        private readonly string _rootPath;
        private readonly int _streamBufferSize;
        private readonly bool _allowNonEmptyDirectoryDelete;
        private readonly bool _flushAfterWrite = true;

        public ServerManagerFileSystemProvider(IOptions<DotNetFileSystemOptions> options,
            IAccountDirectoryQuery accountDirectoryQuery,
            ILogger<ServerManagerFileSystemProvider> logger,
            IServiceProvider serviceProvider)
        {
            _accountDirectoryQuery = accountDirectoryQuery;
            _logger = logger;
            _options = options.Value;
            _serviceProvider = serviceProvider;

            _rootPath = string.IsNullOrEmpty(options.Value.RootPath)
                ? Path.GetTempPath()
                : options.Value.RootPath!;
            _streamBufferSize = options.Value.StreamBufferSize ?? DotNetFileSystem.DefaultStreamBufferSize;
            _allowNonEmptyDirectoryDelete = options.Value.AllowNonEmptyDirectoryDelete;
            //_flushAfterWrite = options.Value;

        }

        public async Task<IUnixFileSystem> Create(IAccountInformation accountInformation)
        {
            var path = _rootPath;
            var directories = _accountDirectoryQuery.GetDirectories(accountInformation);
            if (!string.IsNullOrEmpty(directories.RootPath))
            {
                path = Path.Combine(path, directories.RootPath);
            }

            _logger?.LogDebug("The root directory for {userName} is {rootPath}", accountInformation.FtpUser.Identity.Name, path);

            return new ServerManagerFileSystem(accountInformation, _serviceProvider, path, _allowNonEmptyDirectoryDelete, _streamBufferSize, _flushAfterWrite);
        }
    }
}
