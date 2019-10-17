﻿using CoreRCON.PacketFormats;
using CoreRCON.Parsers;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CoreRCON
{
	public partial class RCON : IDisposable
	{
		internal static string Identifier = "";
		private readonly object _lock = new object();

		// Allows us to keep track of when authentication succeeds, so we can block Connect from returning until it does.
		private TaskCompletionSource<bool> _authenticationTask;

		private bool _connected = false;

		private readonly IPEndPoint _endpoint;

		// When generating the packet ID, use a never-been-used (for automatic packets) ID.
		private int _packetId = 1;

		private readonly string _password;
		private readonly uint _reconnectDelay;
		private readonly int _timeout;

		// Map of pending command references.  These are called when a command with the matching Id (key) is received.  Commands are called only once.
		private Dictionary<int, Action<string>> PendingCommands { get; } = new Dictionary<int, Action<string>>();

		private Socket Tcp { get; set; }

		public event Action OnDisconnected;

		/// <summary>
		/// Initialize an RCON connection and automatically call ConnectAsync().
		/// </summary>
		public RCON(IPAddress host, ushort port, string password, uint reconnectDelay = 30000, int timeout = 10000)
			: this(new IPEndPoint(host, port), password, reconnectDelay, timeout)
		{ }

		/// <summary>
		/// Initialize an RCON connection and automatically call ConnectAsync().
		/// </summary>
		public RCON(IPEndPoint endpoint, string password, uint reconnectDelay = 30000, int timeout = 10000)
		{
			_endpoint = endpoint;
			_password = password;
			_reconnectDelay = reconnectDelay;
            _timeout = timeout;

            // Force manual connections, LIKE IT HAS EVER BEEN
            //ConnectAsync().Wait();
        }

		/// <summary>
		/// Connect to a server through RCON.  Automatically sends the authentication packet.
		/// </summary>
		/// <returns>Awaitable which will complete when a successful connection is made and authentication is successful.</returns>
		public async Task ConnectAsync()
		{
			Tcp = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // Connect to the socket but with a timeout
            var cancellationCompletionSource = new TaskCompletionSource<bool>();

            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter(_timeout);

                var connectAsync = Tcp.ConnectAsync(_endpoint);

                using (cts.Token.Register(() => cancellationCompletionSource.TrySetResult(true)))
                {
                    if (connectAsync != await Task.WhenAny(connectAsync, cancellationCompletionSource.Task))
                    {
                        Tcp.Close();
                        _connected = false;
                        throw new TimeoutException();
                    }
                }
            }


			_connected = true;

			// Set up TCP listener
			var e = new SocketAsyncEventArgs();
			e.Completed += TCPPacketReceived;
			e.SetBuffer(new byte[Constants.MAX_PACKET_SIZE], 0, Constants.MAX_PACKET_SIZE);

			// Start listening for responses
			Tcp.ReceiveAsync(e);

			// Wait for successful authentication
			_authenticationTask = new TaskCompletionSource<bool>();
			await SendPacketAsync(new RCONPacket(0, PacketType.Auth, _password));
			await _authenticationTask.Task;

			Task.Run(() => WatchForDisconnection(_reconnectDelay)).Forget();
		}

		public void Dispose()
		{
			_connected = false;
			Tcp.Shutdown(SocketShutdown.Both);
			Tcp.Dispose();
		}

		/// <summary>
		/// Send a command to the server, and wait for the response before proceeding.  Expect the result to be parseable into T.
		/// </summary>
		/// <typeparam name="T">Type to parse the command as.</typeparam>
		/// <param name="command">Command to send to the server.</param>
		public async Task<T> SendCommandAsync<T>(string command)
			where T : class, IParseable, new()
		{
			Monitor.Enter(_lock);
			var source = new TaskCompletionSource<T>();
			var instance = ParserHelpers.CreateParser<T>();

			var container = new ParserContainer
			{
				IsMatch = line => instance.IsMatch(line),
				Parse = line => instance.Parse(line),
				Callback = parsed => source.SetResult((T)parsed)
			};

			PendingCommands.Add(++_packetId, container.TryCallback);
			var packet = new RCONPacket(_packetId, PacketType.ExecCommand, command);
			Monitor.Exit(_lock);

			await SendPacketAsync(packet);
			return await source.Task;
		}

		/// <summary>
		/// Send a command to the server, and wait for the response before proceeding.  R
		/// </summary>
		/// <param name="command">Command to send to the server.</param>
		public async Task<string> SendCommandAsync(string command)
		{
			Monitor.Enter(_lock);
			var source = new TaskCompletionSource<string>();
			PendingCommands.Add(++_packetId, source.SetResult);
			var packet = new RCONPacket(_packetId, PacketType.ExecCommand, command);
			Monitor.Exit(_lock);

			await SendPacketAsync(packet);
			return await source.Task;
		}

		private void RCONPacketReceived(RCONPacket packet)
		{
            // Call pending result and remove from map
            if (PendingCommands.TryGetValue(packet.Id, out Action<string> action))
            {
                action?.Invoke(packet.Body);
                PendingCommands.Remove(packet.Id);
            }
        }

		/// <summary>
		/// Send a packet to the server.
		/// </summary>
		/// <param name="packet">Packet to send, which will be serialized.</param>
		private async Task SendPacketAsync(RCONPacket packet)
		{
			if (!_connected) throw new InvalidOperationException("Connection is closed.");
			await Tcp.SendAsync(new ArraySegment<byte>(packet.ToBytes()), SocketFlags.None);
		}

		/// <summary>
		/// Event called whenever raw data is received on the TCP socket.
		/// </summary>
		private void TCPPacketReceived(object sender, SocketAsyncEventArgs e)
		{
			// Parse out the actual RCON packet
			RCONPacket packet = RCONPacket.FromBytes(e.Buffer);

			if (packet.Type == PacketType.AuthResponse)
			{
				// Failed auth responses return with an ID of -1
				if (packet.Id == -1)
				{
					throw new AuthenticationException($"Authentication failed for {Tcp.RemoteEndPoint}.");
				}

				// Tell Connect that authentication succeeded
				_authenticationTask.SetResult(true);
			}

			// Forward to handler
			RCONPacketReceived(packet);

			// Continue listening
			if (!_connected) return;
			Tcp.ReceiveAsync(e);
		}

		/// <summary>
		/// Polls the server to check if RCON is still authenticated.  Will still throw if the password was changed elsewhere.
		/// </summary>
		/// <param name="delay">Time in milliseconds to wait between polls.</param>
		private async void WatchForDisconnection(uint delay)
		{
			int checkedDelay = checked((int)delay);

			while (true)
			{
				try
				{
					Identifier = Guid.NewGuid().ToString().Substring(0, 5);
					await SendCommandAsync(Constants.CHECK_STR + Identifier);
				}
				catch (Exception)
				{
					Dispose();
					OnDisconnected();
					return;
				}

				await Task.Delay(checkedDelay);
			}
		}
	}
}