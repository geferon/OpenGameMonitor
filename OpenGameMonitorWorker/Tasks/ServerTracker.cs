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
		private readonly ILogger<ServerTracker> _logger;
		private readonly IServiceProvider _serviceProvider;
		private readonly GameHandlerService _gameHandler;
		private readonly EventHandlerService _eventHandler;
		private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

		private event EventHandler<ServerRecordsEventArgs> ServersMonitorRecorded;

		public ServerTracker(
			ILogger<ServerTracker> logger,
			IServiceProvider serviceProvider,
			GameHandlerService gameHandler,
			EventHandlerService eventHandler,
			Microsoft.Extensions.Configuration.IConfiguration configuration
		)
		{
			_logger = logger;
			_serviceProvider = serviceProvider;
			_gameHandler = gameHandler;
			_eventHandler = eventHandler;
			_configuration = configuration;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			_eventHandler.RegisterHandler("Monitor:ServersMonitorRecordAdded", (handler) => ServersMonitorRecorded += handler.Listener);

			while (!stoppingToken.IsCancellationRequested)
			{
				var timerSeconds = _configuration.GetValue<int>("MonitorSettings:TrackerTimer", 15);
				var nextTime = DateTime.Now;
				var secondsLeft = timerSeconds - (nextTime.Second % timerSeconds);
				nextTime = nextTime.AddTicks(-(nextTime.Ticks % TimeSpan.TicksPerSecond)); // We truncate it
				nextTime = nextTime.AddSeconds(secondsLeft); // And we add the seconds left

				TimeSpan difference = nextTime - DateTime.Now;

				await Task.Delay(difference, stoppingToken);


				_logger.LogDebug("Fetching all servers data to save it in the database");

				using (var scope = _serviceProvider.CreateScope())
				using (var db = scope.ServiceProvider.GetService<MonitorDBContext>())
				{
					var servers = db.Servers
						.Include(s => s.Game)
						.Include(s => s.Owner)
						.Include(s => s.Group)
						.Where(s => s.PID.HasValue);
					//.ToListAsync();

					List<ServerResourceMonitoringRegistry> monitoringEntries = new List<ServerResourceMonitoringRegistry>();

					var cancellationToken = new CancellationTokenSource();
					cancellationToken.CancelAfter(5000); // 5 seconds max

					var writeCustomerBlock = new ActionBlock<Server>(async s =>
					{
						try
						{
							var usageTask = GetServerUsage(s);
							var infoTask = _gameHandler.GetServerInfo(s, cancellationToken.Token);

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
					cancellationToken.Dispose();

					if (monitoringEntries.Count > 0)
					{
						ServersMonitorRecorded?.Invoke(this, new ServerRecordsEventArgs()
						{
							RowsInserted = monitoringEntries.Select(e => e.Id).ToArray()
						});
					}
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
