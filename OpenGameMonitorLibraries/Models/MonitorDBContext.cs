using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace OpenGameMonitorLibraries
{
    class MonitorDBContext : DbContext
    {
        public MonitorDBContext(DbContextOptions<MonitorDBContext> options) : base(options)
        { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Server>(entity =>
            {
                entity.Property(b => b.Graceful)
                    .HasDefaultValue(true);

                entity.Property(b => b.Branch)
                    .HasDefaultValue("public");
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(b => b.Language)
                    .HasDefaultValue("en");
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
}
