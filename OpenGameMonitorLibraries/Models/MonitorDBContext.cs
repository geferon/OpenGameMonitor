using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting.Internal;

namespace OpenGameMonitorLibraries
{
    public class MonitorDBContext : DbContext
    {
        public MonitorDBContext(DbContextOptions<MonitorDBContext> options) : base(options)
        { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Server>(entity =>
            {
				entity.Property(b => b.Enabled)
					.HasDefaultValue(true);

                entity.Property(b => b.Graceful)
                    .HasDefaultValue(true);

                entity.Property(b => b.Branch)
                    .HasDefaultValue("public");
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(b => b.Language)
                    .HasDefaultValue("en");
                entity.Property(b => b.Admin)
                    .HasDefaultValue(false);
            });

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
        }

        public DbSet<Server> Servers { get; set; }
        public DbSet<Game> Games { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<Setting> Settings { get; set; }

        /*protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseMySql("");
        }*/
    }

    // Used for fucking testing

    public class MonitorDBContextFactory : IDesignTimeDbContextFactory<MonitorDBContext>
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

            return new MonitorDBContext(builder.Options);
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

            using (var dbContext = new MonitorDBContext(builder.Options))
            {
                dbContext.Database.EnsureCreated();

                Data = !dbContext.Settings.Any()
                    ? CreateAndSaveDefaultValues(dbContext)
                    : dbContext.Settings.ToDictionary(c => c.Key, c => c.Value);
            }
        }

        public void ForceReload()
        {
            Load();
            OnReload();
        }

        private static IDictionary<string, string> CreateAndSaveDefaultValues(
            MonitorDBContext dbContext)
        {
            // TODO: Set Settings
            var configValues = new Dictionary<string, string>
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
