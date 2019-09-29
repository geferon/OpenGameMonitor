using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OpenGameMonitorLibraries;

namespace OpenGameMonitorWorker
{
    internal class SourceHandler : SteamCMDBaseHandler
    {
        public override string Engine => "source";
        public override event EventHandler ConsoleMessage;

        private Process serverProcess;

        public SourceHandler(IServiceProvider serviceProvider,
            IServiceScopeFactory serviceScopeFactory,
            SteamCMDService steamCMDServ,
            SteamAPIService steamAPIService) : base(serviceProvider, serviceScopeFactory, steamCMDServ, steamAPIService)
        {

        }

        public override async Task InitServer(Server server)
        {
            if (server.PID != default(int))
            {
                try
                {
                    Process ps = Process.GetProcessById(server.PID);
                    ps.OutputDataReceived += (sender, e) =>
                    {
                        ConsoleMessage?.Invoke(server, new ConsoleEventArgs()
                        {
                            NewLine = e.Data
                        });
                    };
                    ps.ErrorDataReceived += (sender, e) =>
                    {
                        ConsoleMessage?.Invoke(server, new ConsoleEventArgs()
                        {
                            NewLine = e.Data,
                            IsError = true
                        });
                    };
                    ps.Exited += (sender, e) =>
                    {
                        serverProcess.Close();
                        serverProcess = null;
                    };

                    serverProcess = ps;
                }
                catch
                {
                    server.PID = default(int);
                    using (var db = _serviceProvider.GetService<MonitorDBContext>())
                    {
                        db.Update(server);
                    }

                    await OpenServer(server);
                }
            }
        }

        public override async Task<bool> IsOpen(Server server)
        {
            return serverProcess != null && !serverProcess.HasExited;
        }

        public override async Task OpenServer(Server server)
        {
            if (serverProcess != null && !serverProcess.HasExited)
            {
                return; // Server is open?
            }

            var proc = new Process();
            proc.StartInfo.FileName = Path.Combine(server.Path, server.Executable);
            proc.StartInfo.WorkingDirectory = Path.GetDirectoryName(Path.Combine(server.Path, server.Executable));
            //proc.StartInfo.Arguments = server.

            proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.UseShellExecute = true;
            proc.EnableRaisingEvents = true; // Investigate?
            //activeSteamCMD.EnableRaisingEvents = false;
            // activeSteamCMD.OutputDataReceived += (sender, eventArgs) => outputStringBuilder.AppendLine(eventArgs.Data);
            // activeSteamCMD.ErrorDataReceived += (sender, eventArgs) => outputStringBuilder.AppendLine(eventArgs.Data);

            proc.OutputDataReceived += (sender, e) =>
            {
                ConsoleMessage?.Invoke(server, new ConsoleEventArgs()
                {
                    NewLine = e.Data
                });
            };
            proc.ErrorDataReceived += (sender, e) =>
            {
                ConsoleMessage?.Invoke(server, new ConsoleEventArgs()
                {
                    NewLine = e.Data,
                    IsError = true
                });
            };

            proc.Exited += (sender, e) =>
            {
                serverProcess.Close();
                serverProcess = null;
            };

            proc.Start();

            serverProcess = proc;
        }

        public override Task CloseServer(Server server)
        {
            throw new NotImplementedException();
        }
    }
}
