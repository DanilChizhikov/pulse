using UnityEngine.Scripting;

namespace DTech.Pulse
{
	[Preserve]
	public enum InitializationSystemStatus
	{
		NotStarted = 0,
		Completed = 1,
		Failed = 2,
		Cancelled = 3,
	}
}
