﻿using IdentityServer4.Events;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGameMonitorLibraries
{
	public static class EventMockupHandlerBase
	{
		public static EventHandler Subscribe(this EventHandler kHandler, EventMockupHandler kElement)
		{
			kHandler += kElement.Listener;
			return kHandler;
		}
	}

	public class EventMockupHandler
	{
		public event EventHandler<EventArgs> InternalEvent;

		public void Listener(object sender, EventArgs e)
		{
			InternalEvent?.Invoke(sender, e);
		}

		public static EventMockupHandler operator +(EventHandler<EventArgs> kHandler, EventMockupHandler kElement)
		{
			kHandler += kElement.Listener;
			return kElement;
		}

		public static EventMockupHandler operator -(EventHandler<EventArgs> kHandler, EventMockupHandler kElement)
		{
			kHandler -= kElement.Listener;
			return kElement;
		}

		public static EventMockupHandler operator +(EventMockupHandler kElement, EventHandler<EventArgs> kHandler)
		{
			kElement.InternalEvent += kHandler;
			return kElement;
		}

		public static EventMockupHandler operator -(EventMockupHandler kElement, EventHandler<EventArgs> kHandler)
		{
			kElement.InternalEvent -= kHandler;
			return kElement;
		}

		/*
		public void ListenType<THandlerType>(EventHandler<THandlerType> kHandler) where THandlerType : EventArgs
		{
			var eventHandler = InternalEvent as EventHandler<THandlerType>;
			eventHandler += kHandler;
		}

		public void RemoveListenerType<THandlerType>(EventHandler<THandlerType> kHandler) where THandlerType : EventArgs
		{
			var eventHandler = InternalEvent as EventHandler<THandlerType>;
			eventHandler -= kHandler;
		}
		*/
	}

	public class EventHandlerService
	{
		private readonly ILogger _logger;

		private readonly Dictionary<string, EventMockupHandler> eventHandlers = new Dictionary<string, EventMockupHandler>();
		private readonly Dictionary<string, List<EventHandler<EventArgs>>> eventListeners = new Dictionary<string, List<EventHandler<EventArgs>>>();
		public EventHandlerService(ILogger<EventHandlerService> logger)
		{
			_logger = logger;
		}

		/*
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
		*/

		public void RegisterHandler(string key, Action<EventMockupHandler> handler)
		{
			if (handler == null)
				throw new ArgumentNullException(nameof(handler));

			//eventHandlers.Add(key, handler);
			if (!eventHandlers.ContainsKey(key))
			{
				eventHandlers.Add(key, new EventMockupHandler());
			}

			var mainHandler = eventHandlers[key];

			handler(mainHandler);

			if (eventListeners.ContainsKey(key))
			{
				foreach (EventHandler<EventArgs> evHandler in eventListeners[key])
				{
					mainHandler += evHandler;
				}
			}
		}

		public EventMockupHandler GetEvent(string key)
		{
			eventHandlers.TryGetValue(key, out EventMockupHandler handler);

			if (handler == null)
			{
				throw new Exception($"No event found with key {key}");
			}

			return handler;
		}

		public EventMockupHandler this[string key]
		{
			get
			{
				return GetEvent(key);
			}
		}

		public void ListenForEvent(string key, EventHandler<EventArgs> callback)
		{
			if (eventHandlers.ContainsKey(key))
			{
				eventHandlers[key] += callback;
			}
			else
			{
				if (!eventListeners.ContainsKey(key))
				{
					eventListeners.Add(key, new List<EventHandler<EventArgs>>());
				}

				eventListeners[key].Add(callback);
			}
		}

		public void ListenForEventType<TEventArgs>(string key, EventHandler<TEventArgs> callback)
			where TEventArgs : EventArgs
		{
			//EventHandler eventHandlerMock = (o, ea) => callback(o, (TEventArgs)ea);

			if (eventHandlers.ContainsKey(key))
			{
				eventHandlers[key] += callback as EventHandler<EventArgs>;
				//eventHandlers[key].ListenType<TEventArgs>(callback);
			}
			else
			{
				if (!eventListeners.ContainsKey(key))
				{
					eventListeners.Add(key, new List<EventHandler<EventArgs>>());
				}

				eventListeners[key].Add(callback as EventHandler<EventArgs>);
			}
		}

		public void StopListeningForEvent(string key, EventHandler<EventArgs> callback)
		{
			if (!eventListeners.ContainsKey(key)) return;

			eventHandlers[key] -= callback;
		}

		public void StopListeningForEvent<TEventArgs>(string key, EventHandler<TEventArgs> callback) where TEventArgs : EventArgs
		{
			if (!eventListeners.ContainsKey(key)) return;

			eventHandlers[key] -= callback as EventHandler<EventArgs>;
			//eventHandlers[key].RemoveListenerType<TEventArgs>(callback);
		}
	}
}
