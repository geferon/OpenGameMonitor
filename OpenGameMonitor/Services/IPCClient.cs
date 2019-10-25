using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenGameMonitorLibraries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xeeny.Api.Client;
using Xeeny.Connections;

namespace OpenGameMonitor
{
    public class IPCClient : BackgroundService
    {
        private readonly ILogger<IPCClient> _logger;
        private readonly IConfiguration _config;
        private readonly EventHandlerService _eventHandlerService;

        private static double RetryTime = 10;

        public IPCClient(ILogger<IPCClient> logger,
            IConfiguration config,
            EventHandlerService eventHandlerService)
        {
            _logger = logger;
            _config = config;
            _eventHandlerService = eventHandlerService;
        }

        public IMonitorComsInterface ComsClient;

        private Task TaskFromCancellationToken(CancellationToken token)
        {
            var task = new TaskCompletionSource<object>();
            token.Register(() => { task.SetResult(null); });
            return task.Task;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var ip = _config.GetValue<String>("MonitorConnection:Address", "localhost");
            var port = _config.GetValue<int>("MonitorConnection:Port", 5001);
            var address = $"tcp://{ip}:{port}/opengameserver";
            var builder = new DuplexConnectionBuilder<IMonitorComsInterface, MonitorComsCallback>(Xeeny.Dispatching.InstanceMode.Single)
                .WithTcpTransport(address, options => {
                    options.Timeout = TimeSpan.FromSeconds(RetryTime);
                });

            builder.CallbackInstanceCreated += (callback) =>
            {
                callback.eventHandlerService = _eventHandlerService;
                callback.Init();
            };

            _logger.LogInformation("Trying to connect to the monitor.");

            while (!cancellationToken.IsCancellationRequested)
            {
                ComsClient = await builder.CreateConnection(false);
                var connection = ((IConnection)ComsClient);

                var task = connection.Connect();

                await Task.WhenAny(task, Task.Delay((int)(RetryTime * 1000)));

                if (connection.State == Xeeny.Transports.ConnectionState.Connected)
                {
                    _logger.LogInformation("Sending initial welcome to the Monitor!");
                    //var connectedTask = ComsClient.Connected();
                    await ComsClient.Connected();
                    _logger.LogInformation("Connected succesfully to the Monitor.");

                    //await Task.WhenAny(connectedTask, TaskFromCancellationToken(cancellationToken));
                    while (!cancellationToken.IsCancellationRequested && connection.State == Xeeny.Transports.ConnectionState.Connected)
                    {
                        await Task.Delay((int)(RetryTime * 1000), cancellationToken);
                    }

                    if (connection.State == Xeeny.Transports.ConnectionState.Connected)
                    {
                        connection.Close();
                    }
                    else if (!cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogWarning("The connection to the Monitor has been lost! Retrying connection in {0} seconds.", RetryTime);
                        await Task.Delay((int)(RetryTime * 1000), cancellationToken);
                    }
                }
                else
                {
                    _logger.LogWarning("Couldn't stablish connection to the Monitor... Retrying again in {0} seconds.", RetryTime);
                    await Task.Delay((int)(RetryTime * 1000), cancellationToken);
                }
            }
        }
    }

    public class ServerEventArgs : EventArgs
    {
        public int ServerID;
    }

    public class ServerMessageEventArgs : EventArgs
    {
        public string Message;
    }

    public class MonitorComsCallback : IMonitorComsCallback
    {
        public EventHandlerService eventHandlerService;

        private event EventHandler panelConfigReloadedEvent;
        private event EventHandler<ServerEventArgs> serverClosedEvent;
        private event EventHandler<ServerMessageEventArgs> serverMessageConsoleEvent;
        private event EventHandler<ServerMessageEventArgs> serverMessageUpdateEvent;
        private event EventHandler<ServerEventArgs> serverOpenedEvent;
        private event EventHandler<ServerEventArgs> serverUpdatedEvent;

        public void Init()
        {
            eventHandlerService.RegisterHandler("Monitor:PanelConfigReloaded", (handler) => panelConfigReloadedEvent += handler.Listener);
            eventHandlerService.RegisterHandler("Monitor:ServerClosed", (handler) => serverClosedEvent += handler.Listener);
            eventHandlerService.RegisterHandler("Monitor:ServerOpened", (handler) => serverOpenedEvent += handler.Listener);
            eventHandlerService.RegisterHandler("Monitor:ServerUpdated", (handler) => serverUpdatedEvent += handler.Listener);
            eventHandlerService.RegisterHandler("Monitor:ServerMessageConsole", (handler) => serverMessageConsoleEvent += handler.Listener);
            eventHandlerService.RegisterHandler("Monitor:ServerMessageUpdate", (handler) => serverMessageUpdateEvent += handler.Listener);
        }

        public async Task PanelConfigReloaded()
        {
            panelConfigReloadedEvent?.Invoke(this, new EventArgs());
        }

        public async Task ServerClosed(int server)
        {
            serverClosedEvent?.Invoke(this, new ServerEventArgs()
            {
                ServerID = server
            });
        }

        public async Task ServerMessageConsole(string message)
        {
            serverMessageConsoleEvent?.Invoke(this, new ServerMessageEventArgs()
            {
                Message = message
            });
        }

        public async Task ServerMessageUpdate(string message)
        {
            serverMessageUpdateEvent?.Invoke(this, new ServerMessageEventArgs()
            {
                Message = message
            });
        }

        public async Task ServerOpened(int server)
        {
            serverOpenedEvent?.Invoke(this, new ServerEventArgs()
            {
                ServerID = server
            });
        }

        public async Task ServerUpdated(int server)
        {
            serverUpdatedEvent?.Invoke(this, new ServerEventArgs()
            {
                ServerID = server
            });
        }
    }
}
