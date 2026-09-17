using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Networking.PlayerConnection;
using UnityEngine;
using UnityEngine.Networking.PlayerConnection;

namespace DTech.Pulse.Editor
{
	[InitializeOnLoad]
	internal static class InitializationGraphDeviceSource
	{
		public static event Action<DeviceSnapshot> OnReceived;
		
		private const int MaxReceivedSnapshots = 20;

		private static readonly List<DeviceSnapshot> _receivedSnapshots = new();

		public static IReadOnlyList<DeviceSnapshot> Snapshots => _receivedSnapshots;

		public static int ConnectedPlayersCount => EditorConnection.instance.ConnectedPlayers?.Count ?? 0;

		static InitializationGraphDeviceSource()
		{
			EditorConnection.instance.Initialize();
			EditorConnection.instance.Register(InitializationGraphRemote.SnapshotMessageId, MessageHandler);
		}
		
		public static void RequestSnapshot()
		{
			EditorConnection.instance.Send(InitializationGraphRemote.RequestMessageId, Array.Empty<byte>());
		}

		private static void MessageHandler(MessageEventArgs args)
		{
			InitializationGraphSnapshot snapshot;
			try
			{
				snapshot = InitializationGraphSnapshot.FromXml(Encoding.UTF8.GetString(args.data));
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				return;
			}

			var received = new DeviceSnapshot(snapshot, args.playerId, DateTime.Now);
			_receivedSnapshots.Insert(0, received);
			if (_receivedSnapshots.Count > MaxReceivedSnapshots)
			{
				_receivedSnapshots.RemoveRange(MaxReceivedSnapshots, _receivedSnapshots.Count - MaxReceivedSnapshots);
			}

			OnReceived?.Invoke(received);
		}
	}
}