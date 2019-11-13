using FubarDev.FtpServer;
using FubarDev.FtpServer.FileSystem;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace OpenGameMonitorWorker
{
    class ServerManagerFileSystemProvider : IFileSystemClassFactory
    {
        public Task<IUnixFileSystem> Create(IAccountInformation accountInformation)
        {
            throw new NotImplementedException();
        }
    }
}
