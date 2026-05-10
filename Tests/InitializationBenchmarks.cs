using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.PerformanceTesting;

namespace DTech.Pulse.Tests
{
	[TestFixture]
	internal sealed class InitializationBenchmarks
	{
		[Test, Performance]
		public void Build_10_UniqueSystems()
		{
			Measure.Method(() =>
				{
					var builder = new InitializationContextBuilder();
					IInitializable[] systems = CreateSystems();
					for (int i = 0; i < systems.Length; i++)
					{
						builder.AddSystem(systems[i]);
					}

					builder.Build();
				})
				.WarmupCount(3)
				.IterationsPerMeasurement(1)
				.MeasurementCount(10)
				.Run();
		}

		[Test, Performance]
		public void Initialize_10_UniqueSystems()
		{
			Measure.Method(() =>
				{
					InitializationContext context = CreateContext();
					context.InitializationAsync(CancellationToken.None)
						.GetAwaiter()
						.GetResult();
				})
				.WarmupCount(3)
				.IterationsPerMeasurement(1)
				.MeasurementCount(10)
				.Run();
		}

		private static InitializationContext CreateContext()
		{
			var builder = new InitializationContextBuilder();
			IInitializable[] systems = CreateSystems();
			for (int i = 0; i < systems.Length; i++)
			{
				builder.AddSystem(systems[i]);
			}

			return builder.Build();
		}

		private static IInitializable[] CreateSystems()
		{
			return new IInitializable[]
			{
				new BenchmarkSystemA(),
				new BenchmarkSystemB(),
				new BenchmarkSystemC(),
				new BenchmarkSystemD(),
				new BenchmarkSystemE(),
				new BenchmarkSystemF(),
				new BenchmarkSystemG(),
				new BenchmarkSystemH(),
				new BenchmarkSystemI(),
				new BenchmarkSystemJ(),
			};
		}

		private sealed class BenchmarkSystemA : IInitializable
		{
			public Task InitializeAsync(CancellationToken token) => Task.CompletedTask;
		}

		private sealed class BenchmarkSystemB : IInitializable
		{
			public Task InitializeAsync(CancellationToken token) => Task.CompletedTask;
		}

		private sealed class BenchmarkSystemC : IInitializable
		{
			public Task InitializeAsync(CancellationToken token) => Task.CompletedTask;
		}

		private sealed class BenchmarkSystemD : IInitializable
		{
			public Task InitializeAsync(CancellationToken token) => Task.CompletedTask;
		}

		private sealed class BenchmarkSystemE : IInitializable
		{
			public Task InitializeAsync(CancellationToken token) => Task.CompletedTask;
		}

		private sealed class BenchmarkSystemF : IInitializable
		{
			public Task InitializeAsync(CancellationToken token) => Task.CompletedTask;
		}

		private sealed class BenchmarkSystemG : IInitializable
		{
			public Task InitializeAsync(CancellationToken token) => Task.CompletedTask;
		}

		private sealed class BenchmarkSystemH : IInitializable
		{
			public Task InitializeAsync(CancellationToken token) => Task.CompletedTask;
		}

		private sealed class BenchmarkSystemI : IInitializable
		{
			public Task InitializeAsync(CancellationToken token) => Task.CompletedTask;
		}

		private sealed class BenchmarkSystemJ : IInitializable
		{
			public Task InitializeAsync(CancellationToken token) => Task.CompletedTask;
		}
	}
}
