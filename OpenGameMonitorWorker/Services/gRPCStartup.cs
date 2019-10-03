using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGameMonitorWorker
{
    class gRPCStartup
    {
        public class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddGrpc();
            }

            public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
            {
                if (env.IsDevelopment())
                {
                    app.UseDeveloperExceptionPage();
                }

                app.UseRouting();

                app.UseEndpoints(endpoints =>
                {
                    // Communication with gRPC endpoints must be made through a gRPC client.
                    // To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909
                    //endpoints.MapGrpcService<GreeterService>();
                });
            }
        }
}
