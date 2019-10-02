//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading;
//using System.Threading.Tasks;
//using JKang.IpcServiceFramework;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging;
//using OpenGameMonitorLibraries;

//namespace OpenGameMonitorWorker
//{
//    public class IPCService : BackgroundService
//    {
//        private readonly ILogger<IPCService> _logger;
//        private readonly IServiceScope _serviceScope;

//        public IPCService(ILogger<IPCService> logger,
//            IServiceScope serviceScope,
//            IServiceProvider services)
//        {
//            _logger = logger;
//            _serviceScope = serviceScope;
//        }

//        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//        {
//                await new IpcServiceHostBuilder(_serviceScope.ServiceProvider)
//                    .AddNamedPipeEndpoint<IMonitorComsInterface>(name: "OpenGameMonitor", pipeName: "Server")
//                    .Build()
//                    .RunAsync(stoppingToken);
//        }
//    }
//}
