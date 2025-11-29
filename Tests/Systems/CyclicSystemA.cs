using System.Threading;
using System.Threading.Tasks;

namespace DTech.Pulse.Tests
{
	internal sealed class CyclicSystemA : IInitializable
	{
		[InitDependency] 
		private CyclicSystemB _b;

		public Task InitializeAsync(CancellationToken token) => Task.CompletedTask;
	}
}