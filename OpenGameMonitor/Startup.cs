using EntityFrameworkCore.Triggers;
using EntityFrameworkCore.Triggers.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SpaServices.AngularCli;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenGameMonitor.Services;
using OpenGameMonitorLibraries;
using OpenGameMonitorWeb;
using OpenGameMonitorWeb.Hubs;
using OpenGameMonitorWeb.Listeners;
using OpenGameMonitorWeb.Policies;
using System;

namespace OpenGameMonitor
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHostedService<IPCClient>();

            services.AddSingleton<EventHandlerService>();

            // DB
            services.AddEntityFrameworkMySql();

            var connectionStr = Configuration.GetConnectionString("MonitorDatabase");

            if (String.IsNullOrEmpty(connectionStr))
            {
                throw new Exception("No connection string has been found!");
                //return;
            }

            services.AddDbContext<MonitorDBContext>(options => options.UseMySql(connectionStr));
            services.AddTriggers();

            services.AddDefaultIdentity<MonitorUser>()
                .AddRoles<MonitorRole>()
                .AddRoleManager<RoleManager<MonitorRole>>()
                .AddEntityFrameworkStores<MonitorDBContext>();

            services.AddIdentityServer()
                .AddApiAuthorization<MonitorUser, MonitorDBContext>(options =>
                {
                    options.Clients.AddIdentityServerSPA("OpenGameMonitorPanel", builder =>
                    {
                        builder.WithRedirectUri("https://localhost:44307/authentication/login-callback");
                        builder.WithLogoutRedirectUri("https://localhost:44307/authentication/logout-callback");
                        //builder.WithRedirectUri("");
                        //builder.WithLogoutRedirectUri("");
                    });
                });

            services.AddAuthentication()
                .AddIdentityServerJwt();

            services.AddAuthorization(options =>
            {
                options.AddPolicy("ServerPolicy", policy => 
                    policy.Requirements.Add(new ServerPolicyRequirement()));
            });

            services.AddSingleton<IAuthorizationHandler, ServerPolicyHandler>();

            services.AddSingleton<HubConnectionManager>();
            services.AddSignalR();
            services.AddHostedService<ServersListener>();

            services.AddControllersWithViews();
            services.AddRazorPages();

            services.AddMvc(options =>
            {
                options.EnableEndpointRouting = false;
            })
                .SetCompatibilityVersion(CompatibilityVersion.Version_3_0);

            // In production, the Angular files will be served from this directory
            services.AddSpaStaticFiles(configuration =>
            {
                configuration.RootPath = "ClientApp/dist";
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IServiceProvider service)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            if (!env.IsDevelopment())
            {
                app.UseSpaStaticFiles();
            }

            app.UseRouting();

            app.UseAuthentication();
            app.UseIdentityServer();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller}/{action=Index}/{id?}");
                endpoints.MapRazorPages();

                endpoints.MapHub<ServersHub>("/servershub");
            });

            app.UseSpa(spa =>
            {
                // To learn more about options for serving an Angular SPA from ASP.NET Core,
                // see https://go.microsoft.com/fwlink/?linkid=864501

                spa.Options.SourcePath = "ClientApp";

                if (env.IsDevelopment())
                {
                    spa.UseAngularCliServer(npmScript: "start");
                }
            });

            service.CreateRoles().Wait();
        }
    }
}
