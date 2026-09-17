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
		public static event Action OnPlayersChanged;

		private const int MaxReceivedSnapshots = 20;

		private static readonly List<DeviceSnapshot> _receivedSnapshots = new();

		public static IReadOnlyList<DeviceSnapshot> Snapshots => _receivedSnapshots;

		public static int ConnectedPlayersCount => EditorConnection.instance.ConnectedPlayers?.Count ?? 0;

		static InitializationGraphDeviceSource()
		{
			EditorConnection.instance.Initialize();
			EditorConnection.instance.Register(InitializationGraphRemote.SnapshotMessageId, MessageHandler);
			EditorConnection.instance.RegisterConnection(PlayerConnectedHandler);
			EditorConnection.instance.RegisterDisconnection(PlayerDisconnectedHandler);
		}

		public static void RequestSnapshot()
		{
			EditorConnection.instance.Send(InitializationGraphRemote.RequestMessageId, Array.Empty<byte>());
		}

		public static void RequestSnapshot(int playerId)
		{
			EditorConnection.instance.Send(InitializationGraphRemote.RequestMessageId, Array.Empty<byte>(), playerId);
		}

		public static string GetPlayerName(int playerId)
		{
			List<ConnectedPlayer> players = EditorConnection.instance.ConnectedPlayers;
			if (players == null)
			{
				return null;
			}

			for (int i = 0; i < players.Count; i++)
			{
				ConnectedPlayer player = players[i];
				if (player != null && player.playerId == playerId)
				{
					return player.name;
				}
			}

			return null;
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

			var received = new DeviceSnapshot(snapshot, args.playerId, GetPlayerName(args.playerId), DateTime.Now);
			_receivedSnapshots.Insert(0, received);
			if (_receivedSnapshots.Count > MaxReceivedSnapshots)
			{
				_receivedSnapshots.RemoveRange(MaxReceivedSnapshots, _receivedSnapshots.Count - MaxReceivedSnapshots);
			}

			OnReceived?.Invoke(received);
		}

		private static void PlayerConnectedHandler(int playerId)
		{
			EditorApplication.delayCall += () => RequestSnapshot(playerId);
			OnPlayersChanged?.Invoke();
		}

		private static void PlayerDisconnectedHandler(int playerId)
		{
			OnPlayersChanged?.Invoke();
		}
	}
}