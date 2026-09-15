using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Scripting;

namespace DTech.Pulse
{
	/// <summary>
	/// Ready-to-run initialization plan built by <see cref="InitializationContextBuilder"/>.
	/// </summary>
	/// <remarks>
	/// Systems are grouped into batches by their dependencies; systems inside one batch run in parallel,
	/// and batches run one after another. A context can be executed only once.
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

		private readonly object _criticalSystemsLock = new();

		private readonly List<ICollection<InitializationNode>> _batches;
		private readonly HashSet<InitializationNode> _criticalSystems;
		private readonly List<InitializationNode> _nodes;
		private readonly InitializationGraphRecorder _graphRecorder;

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

		private bool _isInitializationStarted;
		private bool _isCriticalSystemsInitializedEventInvoked;

		internal InitializationContext(
			List<ICollection<InitializationNode>> batches,
			IEnumerable<InitializationNode> criticalSystems,
			IEnumerable<InitializationNode> nodes,
			InitializationGraphRecorder graphRecorder)
		{
			_batches = batches;
			_criticalSystems = new HashSet<InitializationNode>(criticalSystems);
			_nodes = new List<InitializationNode>(nodes);
			_graphRecorder = graphRecorder;
			TotalSystemsCount = _nodes.Count;
			TotalCriticalSystemsCount = _criticalSystems.Count;
			InitializedSystemsCount = 0;
		}

		/// <summary>
		/// Runs the initialization, batch by batch, until every system is initialized.
		/// </summary>
		/// <param name="token">Token used to cancel the initialization between batches.</param>
		/// <returns>A task that completes when all systems are initialized or the operation is cancelled.</returns>
		/// <exception cref="InvalidOperationException">Thrown when the context has already been run.</exception>
		public async Task InitializationAsync(CancellationToken token)
		{
			if (_isInitializationStarted)
			{
				throw new InvalidOperationException("This initialization context can only be run once.");
			}

			_isInitializationStarted = true;

			var graphStatus = InitializationGraphStatus.Failed;
			_graphRecorder?.Begin();
			try
			{
				for (int i = 0; i < _batches.Count; i++)
				{
					IEnumerable<Task> tasks = _batches[i].Select(node => InitializeNodeAsync(node, token));

					_graphRecorder?.MarkBatchStarted(i);
					await Task.WhenAll(tasks);
					_graphRecorder?.MarkBatchCompleted(i);
					if (token.IsCancellationRequested)
					{
						graphStatus = InitializationGraphStatus.Cancelled;
						return;
					}
				}

				graphStatus = InitializationGraphStatus.Completed;
			}
			catch (OperationCanceledException) when (_graphRecorder != null)
			{
				graphStatus = InitializationGraphStatus.Cancelled;
				throw;
			}
			finally
			{
				_graphRecorder?.Complete(graphStatus);
			}

			_nodes.Clear();
			_batches.Clear();
			_criticalSystems.Clear();
		}

		private void RemoveCriticalSystem(InitializationNode node)
		{
			if (!node.IsCritical)
			{
				return;
			}

			bool shouldInvokeEvent = false;
			lock (_criticalSystemsLock)
			{
				if (!_criticalSystems.Remove(node))
				{
					throw new InvalidOperationException("This critical dependence was not taken into account." +
						"Critical dependencies must be added before initialization begins.");
				}

				if (_criticalSystems.Count == 0 && !_isCriticalSystemsInitializedEventInvoked)
				{
					_isCriticalSystemsInitializedEventInvoked = true;
					shouldInvokeEvent = true;
				}
			}

			InitializedCriticalSystemsCount++;
			if (!shouldInvokeEvent)
			{
				return;
			}

			OnCriticalSystemsInitialized?.Invoke();
		}

		private async Task InitializeNodeAsync(InitializationNode node, CancellationToken token)
		{
			_graphRecorder?.MarkSystemStarted(node);
			try
			{
				await node.InitializeAsync(token, SystemBeginInitializationCallback, SystemInitializationCompleteCallback);
			}
			catch (Exception exception) when (_graphRecorder != null)
			{
				_graphRecorder.MarkSystemFailed(node, exception);
				throw;
			}

			_graphRecorder?.MarkSystemCompleted(node);
			InitializedSystemsCount++;
			RemoveCriticalSystem(node);
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
