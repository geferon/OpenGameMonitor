using FubarDev.FtpServer;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenGameMonitorWorker.Services
{
    class HostedFTPService : IHostedService
    {
        IFtpServerHost _ftpServerHost;

        public HostedFTPService(IFtpServerHost ftpServerHost)
        {
            _ftpServerHost = ftpServerHost;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return _ftpServerHost.StartAsync();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return _ftpServerHost.StopAsync();
        }
    }
}
