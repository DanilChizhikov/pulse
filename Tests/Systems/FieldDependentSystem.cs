using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DTech.Pulse.Tests
{
	internal sealed class FieldDependentSystem : IInitializable
	{
		[InitDependency] 
		private SimpleSystem _dependency;

		public readonly List<string> Log;

		public FieldDependentSystem(List<string> log)
		{
			Log = log;
		}

		public Task InitializeAsync(CancellationToken token)
		{
			Log?.Add(nameof(FieldDependentSystem));
			return Task.CompletedTask;
		}
	}
}