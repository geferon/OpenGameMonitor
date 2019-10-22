using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CoreRCON;
using CoreRCON.PacketFormats;
using Microsoft.Extensions.DependencyInjection;
using OpenGameMonitorLibraries;

namespace OpenGameMonitorWorker
{
    internal class SourceHandler : SteamCMDBaseHandler
    {
        public override string Engine => "source";

        public override event EventHandler ServerClosed;
        public override event EventHandler ServerOpened;
        public override event EventHandler<ConsoleEventArgs> ConsoleMessage;

        private readonly Dictionary<int, Process> serverProcess;

        public SourceHandler(
            IServiceProvider serviceProvider,
            IServiceScopeFactory serviceScopeFactory,
            SteamCMDService steamCMDServ,
            SteamAPIService steamAPIService) :
            base(serviceProvider, serviceScopeFactory, steamCMDServ, steamAPIService)
        {

        }

        private IPEndPoint GetServerEndpoint(Server server)
        {
            var endpoint = new IPEndPoint(
                IPAddress.Parse(GameHandler.GetServerIP(server)),
                server.Port
            );

            return endpoint;
        }

        public override async Task InitServer(Server server)
        {
            if (server.PID != null && server.PID != default)
            {
                try
                {
                    Process ps = Process.GetProcessById((int) server.PID);
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
                        serverProcess[server.Id].Close();
                        serverProcess.Remove(server.Id);

                        ServerClosed?.Invoke(server, e);
                    };

                    serverProcess[server.Id] = ps;
                }
                catch
                {
                    server.PID = default;
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
            return serverProcess.ContainsKey(server.Id) && !serverProcess[server.Id].HasExited;
        }

        public override async Task OpenServer(Server server)
        {
            if (serverProcess.ContainsKey(server.Id) && !serverProcess[server.Id].HasExited)
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
                serverProcess[server.Id].Close();
                serverProcess.Remove(server.Id);

                ServerClosed?.Invoke(server, e);
            };

            proc.Start();

            serverProcess[server.Id] = proc;

            ServerOpened?.Invoke(server, new EventArgs());
        }

        public async override Task CloseServer(Server server)
        {
            var endpoint = GetServerEndpoint(server);
            var config = new SourceConfigParser(server);
            Process serverProc = serverProcess[server.Id];

            bool succesfulShutdown = false;

            if (server.Graceful)
            {
                string rconPass = config.Get("rcon_password");

                if (!String.IsNullOrEmpty(rconPass)) {
                    try
                    {
                        using (var rcon = new RCON(endpoint, rconPass))
                        {
                            await rcon.ConnectAsync();

                            await rcon.SendCommandAsync("quit");

                            succesfulShutdown = true;
                        }
                    }
                    catch (TimeoutException) { }
                    catch (AuthenticationException) { }
                }

                if (succesfulShutdown)
                {
                    using (var cts = new CancellationTokenSource())
                    {
                        cts.CancelAfter(30 * 1000);

                        await serverProc.WaitForExitAsync(cts.Token);

                        if (!serverProc.HasExited)
                        {
                            succesfulShutdown = false;
                        }
                    }
                }
            }

            if (!succesfulShutdown)
            {
                bool success = serverProc.CloseMainWindow();

                if (success)
                {
                    using (var cts = new CancellationTokenSource())
                    {
                        cts.CancelAfter(10 * 1000);

                        await serverProc.WaitForExitAsync(cts.Token);
                    }
                }

                if (!serverProc.HasExited)
                {
                    serverProc.Kill();
                }
            }

            
        }

        public override async Task<object> GetServerInfo(Server server)
        {
            var endpoint = GetServerEndpoint(server);

            var info = await ServerQuery.Info(endpoint, ServerQuery.ServerType.Source) as SourceQueryInfo;

            return info;
        }
        
        public override async Task<object> GetServerPlayers(Server server)
        {
            var endpoint = GetServerEndpoint(server);

            var players = await ServerQuery.Players(endpoint);

            return players;
        }
    }
}
