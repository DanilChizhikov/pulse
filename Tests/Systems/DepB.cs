using System.Threading;
using System.Threading.Tasks;

namespace DTech.Pulse.Tests
{
	internal sealed class DepB : IBaseDep
	{
		public Task InitializeAsync(CancellationToken token) => Task.CompletedTask;
	}
}