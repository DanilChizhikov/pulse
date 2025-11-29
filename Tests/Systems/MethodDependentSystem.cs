using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DTech.Pulse.Tests
{
	internal sealed class MethodDependentSystem : IInitializable
	{
		public readonly List<string> Log;

		public MethodDependentSystem(List<string> log)
		{
			Log = log;
		}

		[InitDependency]
		private void SetDependency(SimpleSystem system) { }

		public Task InitializeAsync(CancellationToken token)
		{
			Log?.Add(nameof(MethodDependentSystem));
			return Task.CompletedTask;
		}
	}
}