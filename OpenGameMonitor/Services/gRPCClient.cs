using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using OpenGameMonitorLibraries;

namespace OpenGameMonitor
{
    public class gRPCClient : IDisposable
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly GrpcChannel _channel;
        private readonly MonitorComs.MonitorComsClient _client;

        public gRPCClient(ILogger<gRPCClient> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            _channel = GrpcChannel.ForAddress("https://localhost:5001");
            _client = new MonitorComs.MonitorComsClient(_channel);
        }

        public void Dispose()
        {
            _channel.Dispose();
        }
    }
}
