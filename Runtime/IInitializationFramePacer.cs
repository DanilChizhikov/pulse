using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Scripting;

namespace DTech.Pulse
{
	/// <summary>
	/// Optional throttling hook that lets the initialization yield when the current frame is already too long.
	/// </summary>
	/// <remarks>
	/// Pulse never creates a pacer on its own: pass one through
	/// <see cref="InitializationContextBuilder.SetFramePacer"/> to enable the throttling.
	/// Without a pacer every system starts as soon as its dependencies are initialized, without any frame gate.
	/// </remarks>
	[Preserve]
	public interface IInitializationFramePacer
	{
		/// <summary>
		/// Whether the current frame is already long enough to postpone the next system.
		/// </summary>
		bool IsFrameOverloaded { get; }

		/// <summary>
		/// Waits until the beginning of the next frame.
		/// </summary>
		/// <param name="token">Token used to abort the waiting.</param>
		/// <returns>
		/// A task that completes on the next frame. Implementations must complete the task instead of throwing
		/// when <paramref name="token"/> is cancelled: the cancellation is handled by
		/// <see cref="InitializationContext"/> itself.
		/// </returns>
		Task WaitNextFrameAsync(CancellationToken token);
	}
}
