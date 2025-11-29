using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DTech.Pulse.Tests
{
	internal sealed class PropertyDependentSystem : IInitializable
	{
		[InitDependency]
		private SimpleSystem Dependency { get; set; }

		public readonly List<string> Log;

		public PropertyDependentSystem(List<string> log)
		{
			Log = log;
		}

		public Task InitializeAsync(CancellationToken token)
		{
			Log?.Add(nameof(PropertyDependentSystem));
			return Task.CompletedTask;
		}
	}
}