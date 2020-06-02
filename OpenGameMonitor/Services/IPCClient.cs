using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using OpenGameMonitorLibraries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xeeny;
using Xeeny.Api.Client;
using Xeeny.Connections;

namespace OpenGameMonitor.Services
{
	public class IPCClient : BackgroundService
	{
		private readonly ILogger<IPCClient> _logger;
		private readonly IConfiguration _config;
		//private readonly EventHandlerService _eventHandlerService;
		private readonly IServiceProvider _serviceProvider;

		private static double RetryTime = 10;

		public IPCClient(ILogger<IPCClient> logger,
			IConfiguration config,
			//EventHandlerService eventHandlerService,
			IServiceProvider serviceProvider)
		{
			_logger = logger;
			_config = config;
			//_eventHandlerService = eventHandlerService;
			_serviceProvider = serviceProvider;
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
			var port = _config.GetValue<int>("MonitorConnection:Port", 5010);
			var address = $"tcp://{ip}:{port}/opengameserver";

			//var builder = new DuplexConnectionBuilder<IMonitorComsInterface, MonitorComsCallback>(Xeeny.Dispatching.InstanceMode.Single)
			var builder = new DuplexConnectionBuilder<IMonitorComsInterface, MonitorComsCallback>(_serviceProvider.GetService<MonitorComsCallback>())
				.WithTcpTransport(address, options => {
					options.Timeout = TimeSpan.FromSeconds(RetryTime);
				});

			builder.CallbackInstanceCreated += (callback) =>
			{
				//callback.eventHandlerService = _eventHandlerService;
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
					if (connection.State == Xeeny.Transports.ConnectionState.Connecting)
					{
						connection.Close();
					}

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

	public class ServerMessageEventArgs : ServerEventArgs
	{
		public string Message;
	}
	public class ServerUpdateProgressEventArgs : ServerEventArgs
	{
		public float Progress;
	}
	public class ServersMonitorRecordsAddedArgs : EventArgs
	{
		public int[] RowsInserted;
	}

	public class MonitorComsCallback : IMonitorComsCallback
	{
		//public EventHandlerService eventHandlerService;

		public event EventHandler PanelConfigReloadedEvent;
		public event EventHandler<ServerEventArgs> ServerClosedEvent;
		public event EventHandler<ServerEventArgs> ServerOpenedEvent;
		public event EventHandler<ServerEventArgs> ServerUpdatedEvent;
		public event EventHandler<ServerEventArgs> ServerUpdateStartedEvent;
		public event EventHandler<ServerMessageEventArgs> ServerMessageConsoleEvent;
		public event EventHandler<ServerMessageEventArgs> ServerMessageUpdateEvent;
		public event EventHandler<ServerUpdateProgressEventArgs> ServerUpdateProgressEvent;
		public event EventHandler<ServersMonitorRecordsAddedArgs> ServersMonitorRecordAddedEvent;

		/*
		public void Init()
		{
			eventHandlerService.RegisterHandler("Monitor:PanelConfigReloaded", (handler) => panelConfigReloadedEvent += handler.Listener);
			eventHandlerService.RegisterHandler("Monitor:ServerClosed", (handler) => serverClosedEvent += handler.Listener);
			eventHandlerService.RegisterHandler("Monitor:ServerOpened", (handler) => serverOpenedEvent += handler.Listener);
			eventHandlerService.RegisterHandler("Monitor:ServerUpdated", (handler) => serverUpdatedEvent += handler.Listener);
			eventHandlerService.RegisterHandler("Monitor:ServerUpdateStarted", (handler) => serverUpdateStartedEvent += handler.Listener);
			eventHandlerService.RegisterHandler("Monitor:ServerMessageConsole", (handler) => serverMessageConsoleEvent += handler.Listener);
			eventHandlerService.RegisterHandler("Monitor:ServerMessageUpdate", (handler) => serverMessageUpdateEvent += handler.Listener);
			eventHandlerService.RegisterHandler("Monitor:ServerUpdateProgress", (handler) => serverUpdateProgressEvent += handler.Listener);
			eventHandlerService.RegisterHandler("Monitor:ServersMonitorRecordAdded", (handler) => serversMonitorRecordsAddedEvent += handler.Listener);
		}
		*/

		public async Task PanelConfigReloaded()
		{
			PanelConfigReloadedEvent?.Invoke(this, new EventArgs());
		}

		public async Task ServerClosed(int server)
		{
			ServerClosedEvent?.Invoke(this, new ServerEventArgs()
			{
				ServerID = server
			});
		}
		public async Task ServerOpened(int server)
		{
			ServerOpenedEvent?.Invoke(this, new ServerEventArgs()
			{
				ServerID = server
			});
		}

		public async Task ServerUpdated(int server)
		{
			ServerUpdatedEvent?.Invoke(this, new ServerEventArgs()
			{
				ServerID = server
			});
		}

		public async Task ServerUpdateStart(int server)
		{
			ServerUpdateStartedEvent?.Invoke(this, new ServerEventArgs()
			{
				ServerID = server
			});
		}

		public async Task ServerMessageConsole(int server, string message)
		{
			ServerMessageConsoleEvent?.Invoke(this, new ServerMessageEventArgs()
			{
				ServerID = server,
				Message = message
			});
		}

		public async Task ServerMessageUpdate(int server, string message)
		{
			ServerMessageUpdateEvent?.Invoke(this, new ServerMessageEventArgs()
			{
				ServerID = server,
				Message = message
			});
		}

		public async Task ServerUpdateProgress(int server, float progress)
		{
			ServerUpdateProgressEvent?.Invoke(this, new ServerUpdateProgressEventArgs()
			{
				ServerID = server,
				Progress = progress
			});
		}

		public async Task ServersMonitorRecordAdded(int[] recordsAdded)
		{
			ServersMonitorRecordAddedEvent?.Invoke(this, new ServersMonitorRecordsAddedArgs()
			{
				RowsInserted = recordsAdded
			});
		}
	}
}
