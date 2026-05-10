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

		private readonly List<ICollection<InitializationNode>> _batches;
		private readonly HashSet<InitializationNode> _criticalSystems;
		private readonly object _criticalSystemsLock = new();
		private readonly List<InitializationNode> _nodes;

		private bool _isInitializationStarted;
		private bool _isCriticalSystemsInitializedEventInvoked;

		internal InitializationContext(
			List<ICollection<InitializationNode>> batches,
			IEnumerable<InitializationNode> criticalSystems,
			IEnumerable<InitializationNode> nodes)
		{
			_batches = batches;
			_criticalSystems = new HashSet<InitializationNode>(criticalSystems);
			_nodes = new List<InitializationNode>(nodes);
		}

		public async Task InitializationAsync(CancellationToken token)
		{
			if (_isInitializationStarted)
			{
				throw new InvalidOperationException("This initialization context can only be run once.");
			}

			_isInitializationStarted = true;

			foreach (ICollection<InitializationNode> batch in _batches)
			{
				IEnumerable<Task> tasks = batch.Select(node => InitializeNodeAsync(node, token));

				await Task.WhenAll(tasks);
				if (token.IsCancellationRequested)
				{
					return;
				}

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

			if (!shouldInvokeEvent)
			{
				return;
			}

			OnCriticalSystemsInitialized?.Invoke();
		}

		private async Task InitializeNodeAsync(InitializationNode node, CancellationToken token)
		{
			await node.InitializeAsync(token, SystemBeginInitializationCallback, SystemInitializationCompleteCallback);
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
