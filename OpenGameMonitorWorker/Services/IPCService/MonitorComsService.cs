using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenGameMonitorLibraries;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGameMonitorWorker
{
    class MonitorComsService : IMonitorComsInterface
    {
        private readonly ILogger _logger;
        private readonly HostBuilderContext _hostBuilderContext;

        public MonitorComsService(ILogger<MonitorComsService> logger,
            HostBuilderContext hostBuilderContext)
        {
            _logger = logger;
            _hostBuilderContext = hostBuilderContext;
        }

        public override void ConfigReloaded()
        {
            // Find MonitorDBConfig
            IConfigurationRoot rootCfg = (IConfigurationRoot)_hostBuilderContext.Configuration;
            MonitorDBConfigurationProvider dbProv = null;
            foreach (IConfigurationProvider provider in rootCfg.Providers)
            {
                if (provider is MonitorDBConfigurationProvider)
                {
                    _logger.LogInformation(":D");

                    dbProv = (MonitorDBConfigurationProvider)provider;
                    break;
                }
            }

            if (dbProv == null)
            {
                return;
            }

            dbProv.ForceReload();
        }
    }
}
