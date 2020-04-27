using EntityFrameworkCore.Triggers;
using EntityFrameworkCore.Triggers.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

            //services.AddDefaultIdentity<MonitorUser>()
            //.AddRoles<MonitorRole>()
            services.AddIdentity<MonitorUser, MonitorRole>(options => options.SignIn.RequireConfirmedAccount = true)
                //.AddRoleManager<RoleManager<MonitorRole>>()
                .AddEntityFrameworkStores<MonitorDBContext>()
                .AddDefaultTokenProviders();

            var builder = services.AddIdentityServer(options =>
                {
                    options.Events.RaiseErrorEvents = true;
                    options.Events.RaiseInformationEvents = true;
                    options.Events.RaiseFailureEvents = true;
                    options.Events.RaiseSuccessEvents = true;
                })
                .AddInMemoryIdentityResources(Config.Ids)
                .AddInMemoryApiResources(Config.Apis)
                .AddInMemoryClients(Config.Clients)
                //.AddAspNetIdentity<MonitorUser>()
                .AddApiAuthorization<MonitorUser, MonitorDBContext>();

            //.AddInMemoryPersistedGrants()
            /*.AddApiAuthorization<MonitorUser, MonitorDBContext>(options =>
            {
                options.Clients.AddIdentityServerSPA("OpenGameMonitorPanel", builder =>
                {
                    builder.WithRedirectUri("https://localhost:44307/authentication/login-callback");
                    builder.WithLogoutRedirectUri("https://localhost:44307/authentication/logout-callback");
                    //builder.WithRedirectUri("");
                    //builder.WithLogoutRedirectUri("");
                });
            });*/


#if DEBUG
            builder.AddDeveloperSigningCredential();
#else
            builder.AddSigningCredentials();
#endif

            services.Configure<IdentityOptions>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 6;

                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;

                options.SignIn.RequireConfirmedEmail = false;
            });

            services.ConfigureApplicationCookie(options =>
            {
                // Cookie settings
                options.Cookie.HttpOnly = true;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(5);

                options.LoginPath = "/Identity/Account/Login";
                options.AccessDeniedPath = "/Identity/Account/AccessDenied";
                options.SlidingExpiration = true;
            });

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    //options.Authority = "http://localhost:";

                    options.Audience = "api1";
                })
                //.AddIdentityServerAuthentication()
                .AddIdentityServerJwt();

            services.AddAuthorization(options =>
            {
                options.AddPolicy("ServerPolicy", policy => 
                    policy.Requirements.Add(new ServerPolicyRequirement()));
            });

            services.AddTransient<IAuthorizationHandler, ServerPolicyHandler>();

            services.AddSingleton<HubConnectionManager>();
            services.AddSignalR();
            services.AddHostedService<ServersListener>();


            services.AddCors(options => options.AddPolicy("AllowAll", p => p.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader()));

            services.AddControllers();
            services.AddControllersWithViews();
            //services.AddRazorPages();

            services.AddMvc(options =>
            {
                options.EnableEndpointRouting = false;
            })
                .SetCompatibilityVersion(CompatibilityVersion.Latest);

            // In production, the Angular files will be served from this directory
            services.AddSpaStaticFiles(configuration =>
            {
                configuration.RootPath = "ClientApp/dist";
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IServiceProvider service)
        {
            app.UseRouting();

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

            app.UseCors("AllowAll");
            app.UseAuthentication();
            app.UseIdentityServer();
            app.UseAuthorization();
            
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapRazorPages();
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller}/{action=Index}/{id?}");

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
