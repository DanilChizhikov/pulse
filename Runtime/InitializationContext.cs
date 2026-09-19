using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Scripting;

namespace DTech.Pulse
{
	/// <summary>
	/// Ready-to-run initialization plan built by <see cref="InitializationContextBuilder"/>.
	/// </summary>
	/// <remarks>
	/// A run has two phases. The first one initializes every critical system (see
	/// <see cref="IInitializationNodeHandle.SetAsCritical"/>); the remaining systems are held back until the last
	/// critical one is done. Inside a phase scheduling is dependency-driven: a system starts as soon as its own
	/// dependencies are initialized, so an unrelated slow system never holds the rest of the phase back.
	/// A context can be executed only once.
	/// </remarks>
	[Preserve]
	public sealed class InitializationContext
	{
		/// <summary>
		/// Raised right before any system starts initializing. Provides the system type.
		/// </summary>
		public event Action<Type> OnSystemInitializationBegan;

		/// <summary>
		/// Raised right after any system finishes initializing. Provides the system type.
		/// </summary>
		public event Action<Type> OnSystemInitializationCompleted;

		/// <summary>
		/// Raised once, when every system marked with <see cref="IInitializationNodeHandle.SetAsCritical"/> is initialized.
		/// </summary>
		public event Action OnCriticalSystemsInitialized;

		private readonly object _gate = new();

		private readonly List<int> _deferredSystems = new();
		private readonly InitializationNode[] _nodes;
		private readonly int[] _blockingDependencies;
		private readonly int[][] _dependents;
		private readonly bool[] _startedSystems;
		private readonly InitializationGraphRecorder _graphRecorder;
		private readonly IInitializationFramePacer _framePacer;

		/// <summary>
		/// Total number of systems in this context.
		/// </summary>
		public int TotalSystemsCount { get; }

		/// <summary>
		/// Total number of systems marked as critical.
		/// </summary>
		public int TotalCriticalSystemsCount { get; }

		/// <summary>
		/// Number of systems already initialized. Useful to drive a loading progress bar.
		/// </summary>
		public int InitializedSystemsCount { get; private set; }

		/// <summary>
		/// Number of critical systems already initialized.
		/// </summary>
		public int InitializedCriticalSystemsCount { get; private set; }

		private TaskCompletionSource<bool> _completionSource;
		private Exception _failure;
		private int _remainingCriticalSystemsCount;
		private int _runningSystemsCount;
		private int _completedSystemsCount;
		private bool _isInitializationStarted;
		private bool _isFailed;
		private bool _isCancelled;
		private bool _isCriticalSystemsInitializedEventInvoked;
		private bool _isCriticalPhaseCompleted;

		internal InitializationContext(
			InitializationNode[] nodes,
			int[] blockingDependencies,
			int[][] dependents,
			int criticalSystemsCount,
			InitializationGraphRecorder graphRecorder,
			IInitializationFramePacer framePacer)
		{
			_nodes = nodes;
			_blockingDependencies = blockingDependencies;
			_dependents = dependents;
			_startedSystems = new bool[nodes.Length];
			_graphRecorder = graphRecorder;
			_framePacer = framePacer;
			TotalSystemsCount = nodes.Length;
			TotalCriticalSystemsCount = criticalSystemsCount;
			InitializedSystemsCount = 0;
			InitializedCriticalSystemsCount = 0;
			_remainingCriticalSystemsCount = criticalSystemsCount;
			_isCriticalPhaseCompleted = criticalSystemsCount == 0;
		}

		/// <summary>
		/// Runs the initialization until every system is initialized.
		/// </summary>
		/// <param name="token">
		/// Token used to cancel the initialization. Once it is cancelled no new system is started, the already
		/// running ones are awaited and the method returns normally.
		/// </param>
		/// <returns>A task that completes when all systems are initialized or the operation is cancelled.</returns>
		/// <exception cref="InvalidOperationException">Thrown when the context has already been run.</exception>
		public async Task InitializationAsync(CancellationToken token)
		{
			if (_isInitializationStarted)
			{
				throw new InvalidOperationException("This initialization context can only be run once.");
			}

			_isInitializationStarted = true;
			_completionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

			var graphStatus = InitializationGraphStatus.Failed;
			_graphRecorder?.Begin();
			try
			{
				await WaitFrameIfOverloadedAsync(token);
				StartReadySystems(token);
				await _completionSource.Task;

				graphStatus = _isCancelled || token.IsCancellationRequested
					? InitializationGraphStatus.Cancelled
					: InitializationGraphStatus.Completed;
			}
			catch (OperationCanceledException)
			{
				graphStatus = InitializationGraphStatus.Cancelled;
				throw;
			}
			finally
			{
				_graphRecorder?.Complete(graphStatus);
			}

			if (graphStatus == InitializationGraphStatus.Completed)
			{
				Array.Clear(_nodes, 0, _nodes.Length);
			}
		}

		private void StartReadySystems(CancellationToken token)
		{
			List<int> readySystems = null;
			lock (_gate)
			{
				for (int i = 0; i < _nodes.Length; i++)
				{
					if (_blockingDependencies[i] != 0)
					{
						continue;
					}

					TryScheduleSystem(i, token, ref readySystems);
				}
			}

			RunSystems(readySystems, token);
		}

		private void UnlockDependents(int index, CancellationToken token)
		{
			int[] dependents = _dependents[index];
			if (dependents.Length == 0)
			{
				return;
			}

			List<int> readySystems = null;
			lock (_gate)
			{
				for (int i = 0; i < dependents.Length; i++)
				{
					int dependentIndex = dependents[i];
					if (--_blockingDependencies[dependentIndex] != 0)
					{
						continue;
					}

					TryScheduleSystem(dependentIndex, token, ref readySystems);
				}
			}

			RunSystems(readySystems, token);
		}

		/// <remarks>Must be called under <see cref="_gate"/>.</remarks>
		private void TryScheduleSystem(int index, CancellationToken token, ref List<int> readySystems)
		{
			if (!_isCriticalPhaseCompleted && !_nodes[index].IsCritical)
			{
				_deferredSystems.Add(index);
				return;
			}

			if (!TryReserveSystem(index, token))
			{
				return;
			}

			(readySystems ??= new List<int>()).Add(index);
		}

		private List<int> ReleaseDeferredSystems(CancellationToken token)
		{
			List<int> readySystems = null;
			lock (_gate)
			{
				for (int i = 0; i < _deferredSystems.Count; i++)
				{
					int index = _deferredSystems[i];
					if (!TryReserveSystem(index, token))
					{
						continue;
					}

					(readySystems ??= new List<int>()).Add(index);
				}

				_deferredSystems.Clear();
			}

			return readySystems;
		}

		private void RunSystems(List<int> systems, CancellationToken token)
		{
			if (systems != null)
			{
				for (int i = 0; i < systems.Count; i++)
				{
					_ = InitializeSystemAsync(systems[i], token);
				}
			}

			TryCompleteInitialization();
		}

		private async Task InitializeSystemAsync(int index, CancellationToken token)
		{
			InitializationNode node = _nodes[index];
			try
			{
				_graphRecorder?.MarkSystemStarted(index);
				await node.InitializeAsync(token, SystemBeginInitializationCallback, SystemInitializationCompleteCallback);
				_graphRecorder?.MarkSystemCompleted(index);

				bool shouldInvokeCriticalSystemsEvent;
				lock (_gate)
				{
					_completedSystemsCount++;
					InitializedSystemsCount++;
					shouldInvokeCriticalSystemsEvent = TryConsumeCriticalSystem(node);
				}

				if (shouldInvokeCriticalSystemsEvent)
				{
					OnCriticalSystemsInitialized?.Invoke();
					RunSystems(ReleaseDeferredSystems(token), token);
				}

				if (_dependents[index].Length > 0)
				{
					await WaitFrameIfOverloadedAsync(token);
					UnlockDependents(index, token);
				}
			}
			catch (Exception exception)
			{
				_graphRecorder?.MarkSystemFailed(index, exception);
				lock (_gate)
				{
					if (exception is OperationCanceledException)
					{
						_isCancelled = true;
					}
					else
					{
						_isFailed = true;
					}

					_failure ??= exception;
				}
			}
			finally
			{
				lock (_gate)
				{
					_runningSystemsCount--;
				}

				TryCompleteInitialization();
			}
		}

		private void TryCompleteInitialization()
		{
			bool isCompleted;
			Exception failure;
			lock (_gate)
			{
				isCompleted = _completedSystemsCount == _nodes.Length ||
					((_isFailed || _isCancelled) && _runningSystemsCount == 0);
				failure = _failure;
			}

			if (!isCompleted)
			{
				return;
			}

			if (failure != null)
			{
				_completionSource.TrySetException(failure);
				return;
			}

			_completionSource.TrySetResult(true);
		}

		private Task WaitFrameIfOverloadedAsync(CancellationToken token)
		{
			if (_framePacer == null || !_framePacer.IsFrameOverloaded)
			{
				return Task.CompletedTask;
			}

			return _framePacer.WaitNextFrameAsync(token) ?? Task.CompletedTask;
		}

		private bool TryReserveSystem(int index, CancellationToken token)
		{
			if (_isFailed || _isCancelled || _startedSystems[index])
			{
				return false;
			}

			if (token.IsCancellationRequested)
			{
				_isCancelled = true;
				return false;
			}

			_startedSystems[index] = true;
			_runningSystemsCount++;
			return true;
		}

		private bool TryConsumeCriticalSystem(InitializationNode node)
		{
			if (!node.IsCritical)
			{
				return false;
			}

			_remainingCriticalSystemsCount--;
			InitializedCriticalSystemsCount++;
			if (_remainingCriticalSystemsCount != 0 || _isCriticalSystemsInitializedEventInvoked)
			{
				return false;
			}

			_isCriticalPhaseCompleted = true;
			_isCriticalSystemsInitializedEventInvoked = true;
			return true;
		}

		private void SystemBeginInitializationCallback(Type systemType)
		{
			OnSystemInitializationBegan?.Invoke(systemType);
		}

		private void SystemInitializationCompleteCallback(Type systemType)
		{
			OnSystemInitializationCompleted?.Invoke(systemType);
		}
	}
}
