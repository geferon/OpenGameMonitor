using AutoMapper;
using EntityFrameworkCore.Triggers;
using EntityFrameworkCore.Triggers.AspNetCore;
using IdentityModel;
using IdentityServer4.Stores;
using Microsoft.AspNetCore.ApiAuthorization.IdentityServer;
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
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using OpenGameMonitor.Services;
using OpenGameMonitorLibraries;
using OpenGameMonitorWeb;
using OpenGameMonitorWeb.Hubs;
using OpenGameMonitorWeb.Listeners;
using OpenGameMonitorWeb.Policies;
using OpenGameMonitorWeb.Services;
using OpenGameMonitorWeb.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.Tasks;

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
            // Gotta add this service as a singleton
            //services.AddHostedService<IPCClient>();
            services.AddSingleton<IPCClient>();
            services.AddSingleton<IHostedService, IPCClient>(serviceProvider => serviceProvider.GetService<IPCClient>());

            services.AddSingleton<EventHandlerService>();

            // DB
            services.AddEntityFrameworkMySql();

            var connectionStr = Configuration.GetConnectionString("MonitorDatabase");

            if (String.IsNullOrEmpty(connectionStr))
            {
                throw new Exception("No connection string has been found!");
                //return;
            }

            services.AddDbContext<MonitorDBContext>(options => options.UseMySql(connectionStr, x => x.MigrationsAssembly("OpenGameMonitorDBMigrations")));
            services.AddTriggers();

            // WTF I have to remove these because if not the identity shit doesn't work??????
            // TODO: Fix this shit!!
            // Observe https://github.com/dotnet/aspnetcore/issues/14160
            System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Remove("nameid");
            System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Remove("sub");

            System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Remove("idp");
            System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Remove("amr");
            // var test = System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultInboundClaimTypeMap;

            services.AddDefaultIdentity<MonitorUser>(options => options.SignIn.RequireConfirmedAccount = true)
                .AddRoles<MonitorRole>()
            /*services.AddIdentity<MonitorUser, MonitorRole>(options => {
                options.SignIn.RequireConfirmedAccount = true;
                //options.ClaimsIdentity.UserIdClaimType = IdentityModel.JwtClaimTypes.Id;
            })*/
                //.AddRoleManager<RoleManager<MonitorRole>>()
                //.AddDefaultUI()
                .AddSignInManager()
                .AddDefaultTokenProviders()
                .AddEntityFrameworkStores<MonitorDBContext>();

            var builder = services.AddIdentityServer(options =>
                {
                    options.Events.RaiseErrorEvents = true;
                    options.Events.RaiseInformationEvents = true;
                    options.Events.RaiseFailureEvents = true;
                    options.Events.RaiseSuccessEvents = true;
                })
                //.AddAspNetIdentity<MonitorUser>()
                .AddApiAuthorization<MonitorUser, MonitorDBContext>(options =>
                {
                    /*
                    options.ApiResources.AddApiResource(
                        "api",
                        spa =>
                        {
                            spa.WithScopes("api");
                        });
                    */

                    options.Clients.AddIdentityServerSPA(
                        "OpenGameMonitorPanel",
                        spa =>
                        {
                            //spa.WithScopes("api")
                            spa.WithRedirectUri("/authentication/login-callback")
                                .WithLogoutRedirectUri("/authentication/logout-callback");
                        });
                })
                .AddProfileService<ProfileService>()
                .AddInMemoryIdentityResources(Config.GetIdentityResources())
                .AddInMemoryApiResources(Config.GetApis())
                .AddInMemoryClients(Config.GetClients());


#if DEBUG
            builder.AddDeveloperSigningCredential();
            //var signingKey = new X509Certificate2(Path.Combine(Directory.GetCurrentDirectory(), "devcert.pfx"), "1234");
            //builder.AddSigningCredential(signingKey);
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

                options.ClaimsIdentity.RoleClaimType = System.Security.Claims.ClaimTypes.Role;
            });

            services.Configure<JwtBearerOptions>(
                IdentityServerJwtConstants.IdentityServerJwtBearerScheme,
                options =>
                {
                    // TODO
                }
            );

            /*
            services.ConfigureApplicationCookie(options =>
            {
                // Cookie settings
                options.Cookie.HttpOnly = true;
                options.ExpireTimeSpan = TimeSpan.FromHours(4);

                options.LoginPath = "/Identity/Account/Login";
                options.AccessDeniedPath = "/Identity/Account/AccessDenied";
                options.SlidingExpiration = true;
            });
            */

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
            //services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.Authority = Configuration["Authority"];
                    options.SaveToken = true;
                    //options.Audience = "api1";
                    options.Audience = "OpenGameMonitorPanel";
                    options.RequireHttpsMetadata = false;

                    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters()
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = services.BuildServiceProvider().GetRequiredService<ISigningCredentialStore>().GetSigningCredentialsAsync().Result.Key,

                        ValidateIssuer = false,
                        ValidateAudience = false,

                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero,
                        RoleClaimType = System.Security.Claims.ClaimTypes.Role
                    };

                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];

                            // If the request is for our hub...
                            var path = context.HttpContext.Request.Path;
                            if (!string.IsNullOrEmpty(accessToken) &&
                                (path.StartsWithSegments("/hubs/servers")))
                            {
                                // Read the token out of the query string
                                context.Token = accessToken;
                            }
                            return Task.CompletedTask;
                        }
                    };
                })
                .AddIdentityServerJwt();
                /*
                .AddOpenIdConnect("oidc", options =>
                {
                    options.Authority = Configuration["Authority"];
#if DEBUG
                    options.RequireHttpsMetadata = false;
#else
                    options.RequireHttpsMetadata = true;
#endif

                    options.ClientId = "spa";
                    options.ClientSecret = "secret";

                    options.SaveTokens = true; // Unknown if necessary?
                    options.GetClaimsFromUserInfoEndpoint = true;

                    options.Events = new Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectEvents()
                    {
                        OnUserInformationReceived = async context =>
                        {
                            if (context.User.RootElement.TryGetProperty(JwtClaimTypes.Role, out JsonElement role))
                            {
                                var claims = new List<Claim>();
                                if (role.ValueKind == JsonValueKind.Array)
                                {
                                    claims.Add(new Claim(JwtClaimTypes.Role, role.GetString()));
                                }
                                else
                                {
                                    foreach (var r in role.EnumerateArray())
                                        claims.Add(new Claim(JwtClaimTypes.Role, r.GetString()));
                                }
                                var id = context.Principal.Identity as ClaimsIdentity;
                                id.AddClaims(claims);
                            }
                        }
                    };
                    options.ClaimActions.MapJsonKey(JwtClaimTypes.Role, "role");
                    options.ClaimActions.MapJsonKey(ClaimTypes.Role, "role");
                }); */
                //.AddCookie(JwtBearerDefaults.AuthenticationScheme);


            services.AddAuthorization(options =>
            {
                options.AddPolicy("ServerPolicy", policy => 
                    policy.Requirements.Add(new ServerPolicyRequirement()));
            });

            services.AddTransient<IAuthorizationHandler, ServerPolicyHandler>();

            services.AddSingleton<HubConnectionManager>();
            services.AddSignalR(options =>
            {
                //options.JsonSerializerOptions.PropertyNamingPolicy = null
            });
            services.AddHostedService<ServersListener>();

            services.AddOptions<Microsoft.AspNetCore.SignalR.JsonHubProtocolOptions>()
                .Configure(x => x.PayloadSerializerOptions = new JsonSerializerOptions()
                {
                    PropertyNamingPolicy = null
                });

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
                .AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = null)
                .SetCompatibilityVersion(CompatibilityVersion.Latest);

            services.AddAutoMapper(typeof(Startup));

            // In production, the Angular files will be served from this directory
            services.AddSpaStaticFiles(configuration =>
            {
                configuration.RootPath = "ClientApp/dist";
            });

            services.AddLogging();
        }


        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IServiceProvider service)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();

                Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
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

            app.UseCors("AllowAll");

            app.UseIdentityServer();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapRazorPages();
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller}/{action=Index}/{id?}");

                endpoints.MapHub<ServersHub>("/hubs/servers");
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

            app.UseMvc();

            /*app.UseTriggers(builder =>
            {

            });*/
        }
    }
}
