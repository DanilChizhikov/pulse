using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Scripting;

namespace DTech.Pulse
{
	/// <summary>
	/// Represents a system that takes part in the initialization pipeline.
	/// </summary>
	/// <remarks>
	/// Dependencies between systems are declared with <see cref="InitDependencyAttribute"/>
	/// or explicitly through <see cref="IInitializationNodeHandle"/>.
	/// </remarks>
	[Preserve]
	public interface IInitializable
	{
		/// <summary>
		/// Performs the asynchronous initialization of the system.
		/// </summary>
		/// <param name="token">Token used to cancel the initialization.</param>
		/// <returns>A task that completes when the system is fully initialized.</returns>
		Task InitializeAsync(CancellationToken token);
	}
}
