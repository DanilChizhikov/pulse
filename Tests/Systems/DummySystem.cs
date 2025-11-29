using System.Threading;
using System.Threading.Tasks;

namespace DTech.Pulse.Tests
{
	internal sealed class DummySystem : IInitializable
	{
		public Task InitializeAsync(CancellationToken token) => Task.CompletedTask;
	}
}