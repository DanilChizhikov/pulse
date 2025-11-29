using System.Threading;
using System.Threading.Tasks;

namespace DTech.Pulse.Tests
{
	internal sealed class CyclicSystemB : IInitializable
	{
		[InitDependency] 
		private CyclicSystemA _a;

		public Task InitializeAsync(CancellationToken token) => Task.CompletedTask;
	}
}