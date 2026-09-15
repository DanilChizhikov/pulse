using UnityEngine.Scripting;

namespace DTech.Pulse
{
	[Preserve]
	public enum InitializationGraphStatus
	{
		Completed = 0,
		Cancelled = 1,
		Failed = 2,
	}
}
