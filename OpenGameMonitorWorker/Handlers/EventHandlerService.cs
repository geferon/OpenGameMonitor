using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGameMonitorWorker
{
	public class EventHandlerService
	{
		private ILogger _logger;

		private Dictionary<string, EventHandler> eventHandlers = new Dictionary<string, EventHandler>();
		private Dictionary<string, List<EventHandler>> eventListeners = new Dictionary<string, List<EventHandler>>();
		public EventHandlerService(ILogger logger)
		{
			_logger = logger;
		}

		public void RegisterHandler(string key, EventHandler handler)
		{
			eventHandlers.Add(key, handler);

			if (eventListeners.ContainsKey(key))
			{
				foreach (EventHandler evHandler in eventListeners[key])
				{
					handler += evHandler;
				}
			}
		}

		public EventHandler GetEvent(string key)
		{
			EventHandler handler;

			eventHandlers.TryGetValue(key, out handler);

			if (handler == null)
			{
				throw new Exception($"No event found with key {key}");
			}

			return handler;
		}

		public EventHandler this[string key]
		{
			get
			{
				return GetEvent(key);
			}
			set
			{
				RegisterHandler(key, value);
			}
		}

		public void ListenForEvent(string key, EventHandler callback)
		{
			if (eventHandlers.ContainsKey(key))
			{
				eventHandlers[key] += callback;
			}
			else
			{
				if (!eventListeners.ContainsKey(key))
				{
					eventListeners.Add(key, new List<EventHandler>());
				}

				eventListeners[key].Add(callback);
			}
		}
	}
}
