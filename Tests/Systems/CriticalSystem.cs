using System.Threading;
using System.Threading.Tasks;

namespace DTech.Pulse.Tests
{
	internal sealed class CriticalSystem : IInitializable
	{
		public bool Initialized { get; private set; }

		public Task InitializeAsync(CancellationToken token)
		{
			Initialized = true;
			return Task.CompletedTask;
		}
	}
}