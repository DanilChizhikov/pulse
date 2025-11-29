using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.PerformanceTesting;

namespace DTech.Pulse.Tests
{
	[TestFixture]
	internal sealed class InitializationBenchmarks
	{
		private sealed class BenchmarkSystem : IInitializable
		{
			public Task InitializeAsync(CancellationToken token) => Task.CompletedTask;
		}

		[Test, Performance]
		public void Build_1000_SimpleSystems()
		{
			const int Count = 1000;

			Measure.Method(() =>
				{
					var builder = new InitializationContextBuilder();
					for (int i = 0; i < Count; i++)
					{
						builder.AddSystem(new BenchmarkSystem());
					}

					builder.Build();
				})
				.WarmupCount(3)
				.IterationsPerMeasurement(1)
				.MeasurementCount(10)
				.Run();
		}

		[Test, Performance]
		public void Initialize_1000_SimpleSystems()
		{
			const int Count = 1000;

			var builder = new InitializationContextBuilder();
			for (int i = 0; i < Count; i++)
			{
				builder.AddSystem(new BenchmarkSystem());
			}

			var context = builder.Build();

			Measure.Method(() =>
				{
					context.InitializationAsync(CancellationToken.None)
						.GetAwaiter()
						.GetResult();
				})
				.WarmupCount(3)
				.IterationsPerMeasurement(1)
				.MeasurementCount(10)
				.Run();
		}
	}
}