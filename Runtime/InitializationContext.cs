using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Scripting;

namespace DTech.Pulse
{
	[Preserve]
	public sealed class InitializationContext
	{
		public event Action<Type> OnSystemInitializationBegan;
		public event Action<Type> OnSystemInitializationCompleted;
		public event Action OnCriticalSystemsInitialized;

		private readonly object _criticalSystemsLock = new();

		private readonly List<ICollection<InitializationNode>> _batches;
		private readonly HashSet<InitializationNode> _criticalSystems;
		private readonly List<InitializationNode> _nodes;
		private readonly InitializationGraphRecorder _graphRecorder;

		public int TotalSystemsCount { get; }
		public int TotalCriticalSystemsCount { get; }
		public int InitializedSystemsCount { get; private set; }
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
