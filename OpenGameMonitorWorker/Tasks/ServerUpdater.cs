using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenGameMonitorLibraries;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenGameMonitorWorker
{
	public class ServerUpdater : BackgroundService
	{
		private readonly IServiceProvider _serviceProvider;
		private readonly ILogger _logger;
		private readonly IConfiguration _config;
		private readonly GameHandler _gameHandler;

		private List<Server> serversToUpdate = new List<Server>();

		public ServerUpdater(IServiceProvider serviceProvider,
			ILogger logger,
			IConfiguration config,
			GameHandler gameHandler)
		{
			_serviceProvider = serviceProvider;
			_logger = logger;
			_config = config;
			_gameHandler = gameHandler;
		}
		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			DateTime? callTime = null;
			if (callTime.HasValue)
				await Delay(callTime.Value - DateTime.Now, stoppingToken);
			else
			{
				callTime = DateTime.Now;
			}
			while (!stoppingToken.IsCancellationRequested)
			{
				if (serversToUpdate.Count > 0)
				{
					Server server = serversToUpdate[0];

					if (server.LastUpdateFailed && DateTime.Now > server.LastUpdate.AddMinutes(_config.GetValue<double>("UpdateSystem:ErrorTimeout", 10)))
					{
						// Server has already been tried to update, stopping
						serversToUpdate.RemoveAt(0);
					}

					try
					{
						await _gameHandler.UpdateServer(server);

						server.LastUpdateFailed = false;
					}
					catch (Exception err)
					{
						_logger.LogError("There has been an error while updating the server {0}! {1}", server.Id, err.Message);

						server.LastUpdateFailed = true;
					}

					server.LastUpdate = DateTime.Now;

					using (var db = _serviceProvider.GetService<MonitorDBContext>())
					{
						db.Update(server);
					}

					serversToUpdate.RemoveAt(0);
				}
				else
				{
					serversToUpdate = await _gameHandler.CheckServerUpdates();

					if (serversToUpdate.Count == 0)
					{
						var nextRun = callTime.Value.Add(TimeSpan.FromMinutes(10)) - DateTime.Now;

						await Delay(nextRun, stoppingToken);
					}
				}
			}
		}
		static async Task Delay(TimeSpan wait, CancellationToken cancellationToken)
		{
			var maxDelay = TimeSpan.FromMilliseconds(int.MaxValue);
			while (wait > TimeSpan.Zero)
			{
				if (cancellationToken.IsCancellationRequested)
					break;
				var currentDelay = wait > maxDelay ? maxDelay : wait;
				await Task.Delay(currentDelay, cancellationToken);
				wait = wait.Subtract(currentDelay);
			}
		}
	}
}
