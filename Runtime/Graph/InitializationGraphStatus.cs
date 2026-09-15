using UnityEngine.Scripting;

namespace DTech.Pulse
{
	/// <summary>
	/// Final status of a recorded initialization run.
	/// </summary>
	[Preserve]
	public enum InitializationGraphStatus
	{
		/// <summary>
		/// Every batch was initialized successfully.
		/// </summary>
		Completed = 0,

		/// <summary>
		/// The run was cancelled through the cancellation token.
		/// </summary>
		Cancelled = 1,

		/// <summary>
		/// A system threw an exception and the run was interrupted.
		/// </summary>
		Failed = 2,
	}
}
