﻿using AutoMapper;
using EntityFrameworkCore.Triggers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenGameMonitor.Services;
using OpenGameMonitorLibraries;
using OpenGameMonitorWeb.Hubs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OpenGameMonitorWeb.Listeners
{
	public class ServersListener : IHostedService //BackgroundService
	{
		private readonly IHubContext<ServersHub> _hub;
		private readonly HubConnectionManager _hubConnectionManager;
		private readonly IMapper _mapper;
		private readonly IServiceProvider _serviceProvider;
		//private readonly IAuthorizationService _authorizationService;
		private readonly EventHandlerService _eventHandlerService;
		public ServersListener(IHubContext<ServersHub> hub,
			HubConnectionManager hubConnectionManager,
			IMapper mapper,
			IServiceProvider serviceProvider,
			//IAuthorizationService authorizationService,
			EventHandlerService eventHandlerService)
		{
			_hub = hub;
			_hubConnectionManager = hubConnectionManager;
			_mapper = mapper;
			_serviceProvider = serviceProvider;
			//_authorizationService = authorizationService;
			_eventHandlerService = eventHandlerService;
		}

		//protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		public async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			_eventHandlerService.ListenForEventType<ServerEventArgs>("Monitor:ServerClosed", ServerClosed);
			_eventHandlerService.ListenForEventType<ServerEventArgs>("Monitor:ServerOpened", ServerOpened);
			_eventHandlerService.ListenForEventType<ServerEventArgs>("Monitor:ServerUpdated", ServerMonitorUpdated);
			_eventHandlerService.ListenForEventType<ServerEventArgs>("Monitor:ServerUpdateStarted", ServerMonitorUpdateStarted);
			_eventHandlerService.ListenForEventType<ServerMessageEventArgs>("Monitor:ServerMessageConsole", ServerMessageConsole);
			_eventHandlerService.ListenForEventType<ServerMessageEventArgs>("Monitor:ServerMessageUpdate", ServerMessageUpdate);
			_eventHandlerService.ListenForEventType<ServerUpdateProgressEventArgs>("Monitor:ServerUpdateProgress", ServerUpdateProgress);
			_eventHandlerService.ListenForEventType<ServersMonitorRecordsAddedArgs>("Monitor:ServersMonitorRecordAdded", ServersMonitorRecordAdded);

			Triggers<Server>.Inserted += ServerInserted;
			Triggers<Server>.Updating += ServerUpdated;
			Triggers<Server>.Deleted += ServerDeleted;

			/*
			while (!stoppingToken.IsCancellationRequested)
			{
				await Task.Delay(5000, stoppingToken);
			}
			*/
		}


		public async Task StartAsync(CancellationToken cancellationToken)
		{
			await ExecuteAsync(cancellationToken);
		}

		public async Task StopAsync(CancellationToken cancellationToken)
		{
			// TODO: UnRegister events
		}

		private async void ServerClosed(object sender, ServerEventArgs args)
		{
			/*
			using (var scope = _serviceProvider.CreateScope())
			using (var db = scope.ServiceProvider.GetService<MonitorDBContext>())
			{
				await NotifyServerUpdated(await db.Servers.FindAsync(args.ServerID), null);
			}
			*/
			await NotifyServerUpdated(args.ServerID, null);
		}

		private async void ServerOpened(object sender, ServerEventArgs args)
		{
			/*
			using (var scope = _serviceProvider.CreateScope())
			using (var db = scope.ServiceProvider.GetService<MonitorDBContext>())
			{
				await NotifyServerUpdated(await db.Servers.FindAsync(args.ServerID), null);
			}
			*/
			await NotifyServerUpdated(args.ServerID, null);
		}

		private async void ServerMonitorUpdated(object sender, ServerEventArgs args)
		{
			/*
			using (var scope = _serviceProvider.CreateScope())
			using (var db = scope.ServiceProvider.GetService<MonitorDBContext>())
			{
				await NotifyServerUpdated(await db.Servers.FindAsync(args.ServerID), null);
			}
			*/
			await NotifyServerUpdated(args.ServerID, null);
		}

		private async void ServerMonitorUpdateStarted(object sender, ServerEventArgs args)
		{
			/*
			using (var scope = _serviceProvider.CreateScope())
			using (var db = scope.ServiceProvider.GetService<MonitorDBContext>())
			{
				await NotifyServerUpdated(await db.Servers.FindAsync(args.ServerID), null);
			}
			*/
			await NotifyServerUpdated(args.ServerID, null);
		}

		private void ServerMessageConsole(object sender, ServerMessageEventArgs e)
		{
			_hub.Clients.Group($"Server:{e.ServerID}").SendAsync("Server:ConsoleMessage", e.ServerID, e.Message);
		}

		private void ServerMessageUpdate(object sender, ServerMessageEventArgs e)
		{
			_hub.Clients.Group($"Server:{e.ServerID}").SendAsync("Server:UpdateMessage", e.ServerID, e.Message);
		}

		private async void ServerUpdateProgress(object sender, ServerUpdateProgressEventArgs e)
		{
			using (var scope = _serviceProvider.CreateScope())
			using (var db = scope.ServiceProvider.GetService<MonitorDBContext>())
			{
				//_hub.Clients.Group($"Server:{e.ServerID}").SendAsync("Server:UpdateProgress", e.Progress);
				// Send to all available clients?
				// TODO: Change
				var server = await db.Servers.FirstOrDefaultAsync(s => s.Id == e.ServerID);
				var connections = await GetDefaultConnections(server);

				//var serverToSend = _mapper.Map<DTOServer>(server);
				if (connections.Length > 0)
					await _hub.Clients.Clients(connections).SendAsync("Server:UpdateProgress", server.Id, e.Progress);
			}
		}

		private async void ServersMonitorRecordAdded(object sender, ServersMonitorRecordsAddedArgs args)
		{
			using (var scope = _serviceProvider.CreateScope())
			using (var db = scope.ServiceProvider.GetService<MonitorDBContext>())
			{
				var records = await db.ServerResourceMonitoring.Include(r => r.Server).Where(r => args.RowsInserted.Contains(r.Server.Id)).ToListAsync();

				// Send them to all the subbed ones
				foreach (var record in records)
				{
					_hub.Clients.Group($"Server:{record.Server.Id}").SendAsync("Server:RecordAdded", record.Server.Id, record);
				}

				/*
				var authService = scope.ServiceProvider.GetService<IAuthorizationService>();

				List<string> connections = new List<string>();
				foreach (var connection in _hubConnectionManager.GetConnectedUsers<ServersHub>())
				{
					var authResult = await authService.AuthorizeAsync(connection.Value, server.Entity, "ServerPolicy");
					if (authResult.Succeeded)
					{
						if (!connections.Contains(connection.Key))
							connections.Add(connection.Key);
					}
				}
				*/
			}
		}

		public async void ServerInserted(IInsertedEntry<Server> server)
		{
			using (var scope = _serviceProvider.CreateScope())
			{
				var authService = scope.ServiceProvider.GetService<IAuthorizationService>();

				List<string> connections = new List<string>();
				foreach (var connection in _hubConnectionManager.GetConnectedUsers<ServersHub>())
				{
					var authResult = await authService.AuthorizeAsync(connection.Value, server.Entity, "ServerPolicy");
					if (authResult.Succeeded)
					{
						if (!connections.Contains(connection.Key))
							connections.Add(connection.Key);
					}
				}

				await NotifyServerInserted(server.Entity, connections.ToArray());
			}
		}

		public async void ServerUpdated(IUpdatingEntry<Server> server)
		{
			using (var scope = _serviceProvider.CreateScope())
			{
				var authService = scope.ServiceProvider.GetService<IAuthorizationService>();

				Dictionary<string, bool[]> connections = new Dictionary<string, bool[]>();
				foreach (var connection in _hubConnectionManager.GetConnectedUsers<ServersHub>())
				{
					if (!connections.ContainsKey(connection.Key))
						connections.Add(connection.Key, new bool[2]);

					connections[connection.Key][0] = (await authService.AuthorizeAsync(connection.Value, server.Original, "ServerPolicy")).Succeeded;
					connections[connection.Key][1] = (await authService.AuthorizeAsync(connection.Value, server.Entity, "ServerPolicy")).Succeeded;
				}

				var allowedConnections = connections.Where(v => v.Value[0] && v.Value[1]).Select(v => v.Key).ToArray();
				var disallowedConnections = connections.Where(v => v.Value[0] && !v.Value[1]).Select(v => v.Key).ToArray();
				var newConnections = connections.Where(v => !v.Value[0] && v.Value[1]).Select(v => v.Key).ToArray();

				List<Task> tasks = new List<Task>();

				tasks.Add(Task.Run(() => NotifyServerUpdated(server.Entity, allowedConnections)));
				tasks.Add(Task.Run(() => NotifyServerDeleted(server.Entity, disallowedConnections)));
				tasks.Add(Task.Run(() => NotifyServerInserted(server.Entity, disallowedConnections)));
				await Task.WhenAll(tasks);
			}
		}

		public async void ServerDeleted(IDeletedEntry<Server> server)
		{
			using (var scope = _serviceProvider.CreateScope())
			{
				var authService = scope.ServiceProvider.GetService<IAuthorizationService>();

				List<string> connections = new List<string>();
				foreach (var connection in _hubConnectionManager.GetConnectedUsers<ServersHub>())
				{
					var authResult = await authService.AuthorizeAsync(connection.Value, server.Entity, "ServerPolicy");
					if (authResult.Succeeded)
					{
						if (!connections.Contains(connection.Key))
							connections.Add(connection.Key);
					}
				}

				await NotifyServerDeleted(server.Entity, connections.ToArray());
			}
		}

		public async Task NotifyServerUpdated(Server server, string[] connections)
		{
			if (connections == null)
				connections = await GetDefaultConnections(server);

			var serverToSend = _mapper.Map<DTOServer>(server);
			if (connections.Length > 0)
				await _hub.Clients.Clients(connections).SendAsync("Server:Updated", serverToSend);
		}

		public async Task NotifyServerUpdated(int serverId, string[] connections)
		{
			using (var scope = _serviceProvider.CreateScope())
			using (var db = scope.ServiceProvider.GetRequiredService<MonitorDBContext>())
			{
				var server = await db.Servers
					.Include(s => s.Owner)
					.Include(s => s.Group)
					.Include(s => s.Game)
					.FirstAsync(s => s.Id == serverId);

				if (connections == null)
					connections = await GetDefaultConnections(server);

				var serverToSend = _mapper.Map<DTOServer>(server);
				if (connections.Length > 0)
					await _hub.Clients.Clients(connections).SendAsync("Server:Updated", serverToSend);
			}
		}

		public async Task NotifyServerDeleted(Server server, string[] connections)
		{
			if (connections == null)
				connections = await GetDefaultConnections(server);

			if (connections.Length > 0)
				await _hub.Clients.Clients(connections).SendAsync("Server:Deleted", server.Id);
		}

		public async Task NotifyServerInserted(Server server, string[] connections)
		{
			if (connections == null)
				connections = await GetDefaultConnections(server);

			var serverToSend = _mapper.Map<DTOServer>(server);
			if (connections.Length > 0)
				await _hub.Clients.Clients(connections).SendAsync("Server:Inserted", serverToSend);
		}

		private async Task PopulateServer(Server server)
		{
			using (var scope = _serviceProvider.CreateScope())
			using (var db = scope.ServiceProvider.GetService<MonitorDBContext>())
			{
				//if (server.Owner == null)
				//    server.Owner = db.
			}
		}

		private async Task<string[]> GetDefaultConnections(Server server)
		{
			using (var scope = _serviceProvider.CreateScope())
			{
				var authService = scope.ServiceProvider.GetService<IAuthorizationService>();

				List<string> connections = new List<string>();
				foreach (var connection in _hubConnectionManager.GetConnectedUsers<ServersHub>())
				{
					var authResult = await authService.AuthorizeAsync(connection.Value, server, "ServerPolicy");
					if (authResult.Succeeded)
					{
						if (!connections.Contains(connection.Key))
							connections.Add(connection.Key);
					}
				}

				return connections.ToArray();
			}
		}
	}
}
