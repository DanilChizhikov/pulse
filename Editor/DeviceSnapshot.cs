using System;
using System.Globalization;

namespace DTech.Pulse.Editor
{
	internal sealed class DeviceSnapshot
	{
		public InitializationGraphSnapshot Snapshot { get; }
		public int PlayerId { get; }
		public DateTime ReceivedAt { get; }
		public string Label { get; }

		public DeviceSnapshot(InitializationGraphSnapshot snapshot, int playerId, DateTime receivedAt)
		{
			Snapshot = snapshot;
			PlayerId = playerId;
			ReceivedAt = receivedAt;
			Label = $"Player {playerId.ToString(CultureInfo.InvariantCulture)} · " +
				receivedAt.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
		}
	}
}