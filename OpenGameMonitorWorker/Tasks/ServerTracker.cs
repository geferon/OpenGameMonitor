using AutoMapper.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using OpenGameMonitorLibraries;
using OpenGameMonitorWorker.Handlers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;

namespace OpenGameMonitorWorker.Tasks
{
	internal class StatsUsage
	{
		public double CPUUsage;
		public long MemoryUsage;
	}

	internal class ServerRecordsEventArgs : EventArgs
	{
		public int[] RowsInserted;
	}

	class ServerTracker : BackgroundService
	{
		private readonly ServiceProvider _serviceProvider;
		private readonly GameHandlerService _gameHandler;
		private readonly EventHandlerService _eventHandler;
		private readonly ILogger<ServerTracker> _logger;
		private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

		private event EventHandler<ServerRecordsEventArgs> ServersMonitorRecorded;

		public ServerTracker(
			ServiceProvider serviceProvider,
			GameHandlerService gameHandler,
			EventHandlerService eventHandler,
			ILogger<ServerTracker> logger,
			Microsoft.Extensions.Configuration.IConfiguration configuration
		) {
			_serviceProvider = serviceProvider;
			_gameHandler = gameHandler;
			_eventHandler = eventHandler;
			_logger = logger;
			_configuration = configuration;

			_eventHandler.RegisterHandler("Monitor:ServersMonitorRecordAdded", (handler) => ServersMonitorRecorded += handler.Listener);
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				var timerSeconds = _configuration.GetValue<int>("MonitorSettings:TrackerTimer", 15);
				var nextTime = DateTime.Now;
				var secondsLeft = timerSeconds + (nextTime.Second % timerSeconds);
				nextTime.AddTicks(-(nextTime.Ticks % TimeSpan.TicksPerSecond)); // We truncate it
				nextTime.AddSeconds(secondsLeft); // And we add the seconds left

				TimeSpan difference = DateTime.Now - nextTime;

				await Task.Delay(difference, stoppingToken);


				_logger.LogDebug("Fetching all servers data to save it in the database");

				using (var scope = _serviceProvider.CreateScope())
				using (var db = scope.ServiceProvider.GetService<MonitorDBContext>())
				{
					var servers = db.Servers
						.Where(s => s.ProcessStatus == ServerProcessStatus.Started);
					//.ToListAsync();

					List<ServerResourceMonitoringRegistry> monitoringEntries = new List<ServerResourceMonitoringRegistry>();

					var writeCustomerBlock = new ActionBlock<Server>(async s =>
					{
						try
						{
							var usageTask = GetServerUsage(s);
							var infoTask = _gameHandler.GetServerInfo(s);

							await Task.WhenAll(usageTask, infoTask);

							var usage = await usageTask;
							var info = await infoTask;

							var entry = new ServerResourceMonitoringRegistry()
							{
								Server = s,
								TakenAt = DateTime.Now,
								CPUUsage = usage.CPUUsage,
								MemoryUsage = usage.MemoryUsage,
								ActivePlayers = info.Players
							};

							await db.ServerResourceMonitoring.AddAsync(entry);
							monitoringEntries.Add(entry);
						}
						catch (Exception err)
						{
							_logger.LogError(err, "There has been an error while fetching the data of server with id {0}", s.Id);
							// Ignore, continue with the others
						}
					});

					await servers.ForEachAsync(async s =>
					{
						await writeCustomerBlock.SendAsync(s);
					});


					writeCustomerBlock.Complete();
					await writeCustomerBlock.Completion;

					await db.SaveChangesAsync();

					_logger.LogDebug("All servers information fetched and saved!");

					ServersMonitorRecorded?.Invoke(this, new ServerRecordsEventArgs()
					{
						RowsInserted = monitoringEntries.Select(e => e.Id).ToArray()
					});
				}
			}
		}

		private async Task<StatsUsage> GetServerUsage(Server server)
		{
			Process process = Process.GetProcessById(server.PID.Value);

			var cpuUsage = await GetProcessCPUUsage(process); // Percentage
			var memoryUsage = process.WorkingSet64 / (1024 ^ 2); // In Megabytes

			return new StatsUsage()
			{
				CPUUsage = cpuUsage,
				MemoryUsage = memoryUsage
			};
		}

		private async Task<double> GetProcessCPUUsage(Process process)
		{
			var startTime = DateTime.UtcNow;
			var startCpuUsage = process.TotalProcessorTime;

			await Task.Delay(500);

			var endTime = DateTime.UtcNow;
			var endCpuUsage = process.TotalProcessorTime;

			var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
			var totalMsPassed = (endTime - startTime).TotalMilliseconds;

			var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);

			return cpuUsageTotal * 100;
		}
	}
}
