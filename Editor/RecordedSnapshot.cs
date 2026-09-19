using System;
using System.Globalization;

namespace DTech.Pulse.Editor
{
	internal sealed class RecordedSnapshot
	{
		public InitializationGraphSnapshot Snapshot { get; }
		public int Ordinal { get; }
		public DateTime RecordedAt { get; }
		public string Label { get; }

		public RecordedSnapshot(InitializationGraphSnapshot snapshot, int ordinal, DateTime recordedAt)
		{
			Snapshot = snapshot;
			Ordinal = ordinal;
			RecordedAt = recordedAt;
			Label = $"#{ordinal.ToString(CultureInfo.InvariantCulture)} · " +
				$"{recordedAt.ToString("HH:mm:ss", CultureInfo.InvariantCulture)} · {snapshot.Status}";
		}
	}
}
