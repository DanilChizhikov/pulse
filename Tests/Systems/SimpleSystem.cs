using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DTech.Pulse.Tests
{
	internal sealed class SimpleSystem : IInitializable
	{
		public readonly List<string> Log;

		public SimpleSystem(List<string> log)
		{
			Log = log;
		}

		public Task InitializeAsync(CancellationToken token)
		{
			Log?.Add(nameof(SimpleSystem));
			return Task.CompletedTask;
		}
	}
}