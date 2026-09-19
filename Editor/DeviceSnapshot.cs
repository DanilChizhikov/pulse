using System;
using System.Globalization;

namespace DTech.Pulse.Editor
{
	internal sealed class DeviceSnapshot
	{
		public InitializationGraphSnapshot Snapshot { get; }
		public int PlayerId { get; }
		public string DeviceName { get; }
		public DateTime ReceivedAt { get; }
		public string Label { get; }

		public DeviceSnapshot(InitializationGraphSnapshot snapshot, int playerId, string deviceName, DateTime receivedAt)
		{
			Snapshot = snapshot;
			PlayerId = playerId;
			DeviceName = string.IsNullOrEmpty(deviceName)
				? $"Player {playerId.ToString(CultureInfo.InvariantCulture)}"
				: deviceName;

			ReceivedAt = receivedAt;
			Label = $"{DeviceName} · {receivedAt.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}";
		}
	}
}