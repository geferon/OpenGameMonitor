//using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging;
//using OpenGameMonitorLibraries;
//using System;
//using System.Collections.Generic;
//using System.Globalization;
//using System.Linq;
//using System.Net;
//using System.Reflection;
//using System.Text;
//using System.Threading.Tasks;
//using Google.Protobuf.WellKnownTypes;
//using Grpc.Core;

//namespace OpenGameMonitorWorker
//{
//    //class MonitorComsService : IMonitorComsInterface
//    public class MonitorComsService : OpenGameMonitorLibraries.MonitorComs.MonitorComsBase
//    {
//        private readonly ILogger _logger;
//        private readonly IServiceProvider _serviceProvider;
//        private readonly HostBuilderContext _hostBuilderContext;
//        private readonly GameHandler _gameHandler;

//        public MonitorComsService(ILogger<MonitorComsService> logger,
//            IServiceProvider serviceProvider,
//            HostBuilderContext hostBuilderContext,
//            GameHandler handler)
//        {
//            _logger = logger;
//            _serviceProvider = serviceProvider;
//            _hostBuilderContext = hostBuilderContext;
//            _gameHandler = handler;
//        }

//        private OpenGameMonitorLibraries.Server GetServer(ServerID server)
//        {
//            using (MonitorDBContext db = (MonitorDBContext)_serviceProvider.GetService(typeof(MonitorDBContext)))
//            //using (var db = _serviceProvider.GetService<MonitorDBContext>())
//            {
//                /*List<OpenGameMonitorLibraries.Server> servers = db.Servers
//                    .Where(r => r.PID != null && r.PID != default(int))
//                    .ToList();*/

//                return db.Servers.Where(r => r.Id == server.Id)
//                    .FirstOrDefault<OpenGameMonitorLibraries.Server>();
//            }
//        }

//        private IGameHandlerBase GetServerHandler(OpenGameMonitorLibraries.Server server)
//        {
//            return _gameHandler.GetServerHandler(server);
//        }

//        private IGameHandlerBase GetServerHandler(ServerID serverId)
//        {
//            OpenGameMonitorLibraries.Server server = GetServer(serverId);

//            return GetServerHandler(server);
//        }

//        // Google.Protobuf.WellKnownTypes.Empty
//        public override async Task<Empty> PanelConfigReloaded(ConfigReloadedParams prms, ServerCallContext context)
//        {
//            // Find MonitorDBConfig
//            IConfigurationRoot rootCfg = (IConfigurationRoot)_hostBuilderContext.Configuration;
//            MonitorDBConfigurationProvider dbProv = null;
//            foreach (IConfigurationProvider provider in rootCfg.Providers)
//            {
//                if (provider is MonitorDBConfigurationProvider)
//                {
//                    _logger.LogInformation(":D");

//                    dbProv = (MonitorDBConfigurationProvider)provider;
//                    break;
//                }
//            }

//            if (dbProv != null)
//            {
//                dbProv.ForceReload();
//            }

//            return new Empty();
//        }

//        // Just events, nothing to do here for now?
//        /*
//        public override async Task<Empty> ServerOpened(ServerID serverId, ServerCallContext context)
//        {
//            return new Empty();
//        }
//        public override async Task<Empty> ServerClosed(ServerID serverId, ServerCallContext context)
//        {
//            return new Empty();
//        }
//        */

        
//        public override async Task<ServerActionResult> ServerOpen(ServerID serverId, ServerCallContext context)
//        {
//            var server = GetServer(serverId);
//            var svHandler = GetServerHandler(server);

//            await svHandler.OpenServer(server);

//            return new ServerActionResult();
//        }
//        public override async Task<ServerActionResult> ServerClose(ServerID serverId, ServerCallContext context)
//        {
//            var server = GetServer(serverId);
//            var svHandler = GetServerHandler(server);

//            await svHandler.CloseServer(server);

//            return new ServerActionResult();
//        }

//        /*
//        public override async Task<Empty> ServerUpdated(ServerID serverId, ServerCallContext context)
//        {
//            return new Empty();
//        }
//        */

//        public override async Task<ServerActionResult> ServerUpdate(ServerID serverId, ServerCallContext context)
//        {
//            var server = GetServer(serverId);
//            var svHandler = GetServerHandler(server);

//            await svHandler.UpdateServer(server);

//            return new ServerActionResult();
//        }

//        /*
//        public override async Task<Empty> ServerConsoleMessage(ServerMessage serverMsg, ServerCallContext context)
//        {
//            return new Empty();
//        }
//        public override async Task<Empty> ServerUpdateMessage(ServerMessage serverMsg, ServerCallContext context)
//        {
//            return new Empty();
//        }
//        */
//    }
//}
