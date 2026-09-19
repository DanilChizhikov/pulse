#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.Networking.PlayerConnection;
using UnityEngine.Scripting;

namespace DTech.Pulse
{
	[Preserve]
	internal static class InitializationGraphRemote
	{
		public static readonly Guid SnapshotMessageId = new("f384097e0e0c4a5dbf15298d4fdd4b88");
		public static readonly Guid RequestMessageId = new("35744cf432f64d9d87ff818a19c211be");

		private static readonly ConcurrentQueue<string> _pendingSnapshots = new();

		private static int _mainThreadId = -1;
		private static string _lastSnapshotXml;
		private static PlayerLoopSystem _originalPlayerLoop;
		private static bool _isHooked;
		
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
		private static void Initialize()
		{
			if (Application.isEditor)
			{
				return;
			}

			_mainThreadId = Thread.CurrentThread.ManagedThreadId;
			PlayerConnection.instance.Register(RequestMessageId, RequestHandler);
			Hook();
		}

		public static void Publish(InitializationGraphSnapshot snapshot)
		{
			if (snapshot == null || Application.isEditor)
			{
				return;
			}

			string xml = InitializationGraphXmlReport.Build(snapshot);
			_lastSnapshotXml = xml;

			if (Thread.CurrentThread.ManagedThreadId == _mainThreadId)
			{
				Send(xml);
				return;
			}

			_pendingSnapshots.Enqueue(xml);
		}

		private static void RequestHandler(MessageEventArgs args)
		{
			string xml = _lastSnapshotXml;
			if (!string.IsNullOrEmpty(xml))
			{
				Send(xml);
			}
		}

		private static void Send(string xml)
		{
			PlayerConnection connection = PlayerConnection.instance;
			if (!connection.isConnected)
			{
				return;
			}

			try
			{
				connection.Send(SnapshotMessageId, Encoding.UTF8.GetBytes(xml));
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}
		}

		private static void TickFrame()
		{
			while (_pendingSnapshots.TryDequeue(out string xml))
			{
				Send(xml);
			}
		}

		private static void Hook()
		{
			if (_isHooked)
			{
				return;
			}

			PlayerLoopSystem originalPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
			PlayerLoopSystem modifiedPlayerLoop = originalPlayerLoop;
			var remoteSystem = new PlayerLoopSystem
			{
				type = typeof(InitializationGraphRemote),
				updateDelegate = TickFrame,
			};

			if (!PlayerLoopUtilities.TryAppendSubSystem(ref modifiedPlayerLoop, typeof(UnityEngine.PlayerLoop.Update), remoteSystem))
			{
				return;
			}

			_originalPlayerLoop = originalPlayerLoop;
			PlayerLoop.SetPlayerLoop(modifiedPlayerLoop);
			_isHooked = true;
			Application.quitting += ApplicationQuittingHandler;
		}

		private static void Unhook()
		{
			if (!_isHooked)
			{
				return;
			}

			_isHooked = false;
			Application.quitting -= ApplicationQuittingHandler;
			PlayerLoop.SetPlayerLoop(_originalPlayerLoop);
		}

		private static void ApplicationQuittingHandler()
		{
			TickFrame();
			Unhook();
			PlayerConnection.instance.Unregister(RequestMessageId, RequestHandler);
		}
	}
}
#endif