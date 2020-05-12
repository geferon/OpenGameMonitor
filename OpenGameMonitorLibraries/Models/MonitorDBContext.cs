using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using IdentityServer4.EntityFramework.Options;
using EntityFrameworkCore.Triggers;
using IdentityServer4.EntityFramework.Interfaces;
using IdentityServer4.EntityFramework.Entities;
using System.Threading.Tasks;
using IdentityServer4.EntityFramework.Extensions;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Diagnostics;
using System.Threading;

namespace OpenGameMonitorLibraries
{
	public class MonitorDBContext : ExtendedApiAuthorizationDbContext<MonitorUser, MonitorRole, string>
	{
		public MonitorDBContext(DbContextOptions<MonitorDBContext> options,
			IOptions<OperationalStoreOptions> operationalStoreOptions)
			: base(options, operationalStoreOptions)
		{ }

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<Server>(entity =>
			{
				entity.Property(b => b.Enabled)
					.HasDefaultValue(true);

				entity.Property(b => b.Graceful)
					.HasDefaultValue(true);

				entity.Property(b => b.RestartOnClose)
					.HasDefaultValue(true);

				entity.Property(b => b.StartParamsModifyAllowed)
					.HasDefaultValue(true);

				entity.Property(b => b.ProcessPriority)
					.HasDefaultValue(ProcessPriorityClass.Normal);

				entity.Property(b => b.EnvironmentVariables)
					.HasConversion(
						v => System.Text.Json.JsonSerializer.Serialize(v, null),
						v => System.Text.Json.JsonSerializer.Deserialize<List<ServerEnvironmentVariable>>(v, null)
					);

				entity.Property(b => b.LastUpdateFailed)
					.HasDefaultValue(false);

				entity.Property(b => b.Branch)
					.HasDefaultValue("public");

				//entity.Property(b => b.Created)
				//    .HasDefaultValueSql("CURRENT_TIMESTAMP");
			});

			//modelBuilder.Entity<User>(entity =>
			//{
			//    entity.Property(b => b.Language)
			//        .HasDefaultValue("en");
			//    entity.Property(b => b.Admin)
			//        .HasDefaultValue(false);
			//});
			
			modelBuilder.Entity<GroupUser>(entity =>
			{
				entity.HasKey(b => new { b.UserID, b.GroupID });
				entity.HasOne(b => b.Group)
					.WithMany(b => b.Members)
					.HasForeignKey(b => b.GroupID);
				entity.HasOne(b => b.User)
					.WithMany(b => b.Groups)
					.HasForeignKey(b => b.UserID);
			});

			modelBuilder.Entity<Setting>(entity =>
			{
				entity.Property(b => b.Value)
					.HasConversion(
						v => System.Text.Json.JsonSerializer.Serialize(v, null),
						v => System.Text.Json.JsonSerializer.Deserialize<object>(v, null)
					);
			});
		}

		public DbSet<Server> Servers { get; set; }
		public DbSet<Game> Games { get; set; }
		//public DbSet<User> Users { get; set; }
		public DbSet<Group> Groups { get; set; }
		public DbSet<Setting> Settings { get; set; }

		/*protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
		{
			optionsBuilder.UseMySql("");
		}*/
	}

	// Used for fucking testing

	public class MonitorDBContextFactory : DbContextWithTriggers, IDesignTimeDbContextFactory<MonitorDBContext>
	{
		public MonitorDBContext CreateDbContext(string[] args)
		{
			//var env = new HostingEnvironment();
			string envName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

			var configBuilder = new ConfigurationBuilder()
				.SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../OpenGameMonitorWorker"))
				.AddJsonFile("appsettings.json",
					optional: false,
					reloadOnChange: true)
				.AddJsonFile($"appsettings.{envName}.json",
					optional: true)
				.AddEnvironmentVariables();

			var config = configBuilder.Build();

			var builder = new DbContextOptionsBuilder<MonitorDBContext>();
			builder.UseMySql(config.GetConnectionString("MonitorDatabase"),
				optionsBuilder => optionsBuilder.MigrationsAssembly(typeof(MonitorDBContext).GetTypeInfo().Assembly.GetName().Name));

			return new MonitorDBContext(builder.Options, Options.Create(new OperationalStoreOptions()));
		}
	}

	public class MonitorDBConfigurationSource : IConfigurationSource
	{
		private readonly Action<DbContextOptionsBuilder> _optionsAction;

		public MonitorDBConfigurationSource(Action<DbContextOptionsBuilder> optionsAction)
		{
			_optionsAction = optionsAction;
		}

		public IConfigurationProvider Build(IConfigurationBuilder builder)
		{
			return new MonitorDBConfigurationProvider(_optionsAction);
		}
	}

	public class MonitorDBConfigurationProvider : ConfigurationProvider
	{
		public MonitorDBConfigurationProvider(Action<DbContextOptionsBuilder> optionsAction)
		{
			OptionsAction = optionsAction;
		}

		Action<DbContextOptionsBuilder> OptionsAction { get; }

		// Load config data from EF DB.
		public override void Load()
		{
			var builder = new DbContextOptionsBuilder<MonitorDBContext>();

			OptionsAction(builder);

			using (var dbContext = new MonitorDBContext(builder.Options, Options.Create(new OperationalStoreOptions())))
			{
				//dbContext.Database.EnsureCreated();
				try
				{
					Data = (!dbContext.Settings.Any()
						? CreateAndSaveDefaultValues(dbContext)
						: dbContext.Settings.ToDictionary(c => c.Key, c => c.Value))
						.Select(pair => new KeyValuePair<string, string>(
							pair.Key,
							//System.Text.Json.JsonSerializer.Serialize(pair.Value, null)
							pair.Value.ToString()
						))
						.ToDictionary(x => x.Key, x => x.Value);
				}
				catch (Exception err)
				{
					// Cannot connect to DB for EF Config
				}
			}
		}

		public void ForceReload()
		{
			Load();
			OnReload();
		}

		private static IDictionary<string, object> CreateAndSaveDefaultValues(
			MonitorDBContext dbContext)
		{
			// TODO: Set Settings
			var configValues = new Dictionary<string, object>
			{
				{ "quote1", "I aim to misbehave." },
				{ "quote2", "I swallowed a bug." },
				{ "quote3", "You can't stop the signal, Mal." }
			};

			dbContext.Settings.AddRange(configValues
				.Select(kvp => new Setting
				{
					Key = kvp.Key,
					Value = kvp.Value
				})
				.ToArray());

			dbContext.SaveChanges();

			return configValues;
		}
	}

	/// <summary>
	/// Database abstraction for a combined <see cref="DbContext"/> using ASP.NET Identity and Identity Server.
	/// </summary>
	/// <typeparam name="TUser"></typeparam>
	public class ExtendedApiAuthorizationDbContext<TUser, TRole, TKey> : IdentityDbContext<TUser, TRole, TKey>, IPersistedGrantDbContext
		where TUser : IdentityUser<TKey>
		where TRole : IdentityRole<TKey>
		where TKey : IEquatable<TKey>
	{
		private readonly IOptions<OperationalStoreOptions> _operationalStoreOptions;

		/// <summary>
		/// Initializes a new instance of <see cref="ApiAuthorizationDbContext{TUser}"/>.
		/// </summary>
		/// <param name="options">The <see cref="DbContextOptions"/>.</param>
		/// <param name="operationalStoreOptions">The <see cref="IOptions{OperationalStoreOptions}"/>.</param>
		public ExtendedApiAuthorizationDbContext(
			DbContextOptions options,
			IOptions<OperationalStoreOptions> operationalStoreOptions)
			: base(options)
		{
			_operationalStoreOptions = operationalStoreOptions;
		}

		/// <summary>
		/// Gets or sets the <see cref="DbSet{PersistedGrant}"/>.
		/// </summary>
		public DbSet<PersistedGrant> PersistedGrants { get; set; }

		/// <summary>
		/// Gets or sets the <see cref="DbSet{DeviceFlowCodes}"/>.
		/// </summary>
		public DbSet<DeviceFlowCodes> DeviceFlowCodes { get; set; }

		Task<int> IPersistedGrantDbContext.SaveChangesAsync() => base.SaveChangesAsync();

		/// <inheritdoc />
		protected override void OnModelCreating(ModelBuilder builder)
		{
			base.OnModelCreating(builder);
			builder.ConfigurePersistedGrantContext(_operationalStoreOptions.Value);
		}

		public override Int32 SaveChanges() {
			return this.SaveChangesWithTriggers(base.SaveChanges, acceptAllChangesOnSuccess: true);
		}
		public override Int32 SaveChanges(Boolean acceptAllChangesOnSuccess) {
			return this.SaveChangesWithTriggers(base.SaveChanges, acceptAllChangesOnSuccess);
		}
		public override Task<Int32> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken)) {
			return this.SaveChangesWithTriggersAsync(base.SaveChangesAsync, acceptAllChangesOnSuccess: true, cancellationToken: cancellationToken);
		}
		public override Task<Int32> SaveChangesAsync(Boolean acceptAllChangesOnSuccess, CancellationToken cancellationToken = default(CancellationToken)) {
			return this.SaveChangesWithTriggersAsync(base.SaveChangesAsync, acceptAllChangesOnSuccess, cancellationToken);
		}
	}

	public static class EntityFrameworkExtensions
	{
		public static IConfigurationBuilder AddMonitorDBConfiguration(
			this IConfigurationBuilder builder,
			Action<DbContextOptionsBuilder> optionsAction)
		{
			return builder.Add(new MonitorDBConfigurationSource(optionsAction));
		}
	}
}
