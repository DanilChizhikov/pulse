using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DTech.Pulse
{
	public sealed class InitializationContext
	{
		public event Action<Type> OnSystemInitializationBegan;
		public event Action<Type> OnSystemInitializationCompleted;
		public event Action OnCriticalSystemsInitialized;
		
		private readonly List<ICollection<InitializationNode>> _batches;
		private readonly HashSet<InitializationNode> _criticalSystems;
		private readonly List<InitializationNode> _nodes;
		
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
			for (int i = 0; i < _nodes.Count; i++)
			{
				InitializationNode node = _nodes[i];
				node.OnStartInitialize(SystemBeginInitializationCallback);
				node.OnCompleteInitialize(SystemInitializationCompleteCallback);
			}

			foreach (ICollection<InitializationNode> batch in _batches)
			{
				IEnumerable<Task> tasks = batch.Select(node => node.InitializeAsync(token));
				
				await Task.WhenAll(tasks);
				if (token.IsCancellationRequested)
				{
					return;
				}

				foreach (InitializationNode node in batch)
				{
					RemoveCriticalSystem(node);
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

			if(!_criticalSystems.Remove(node))
			{
				throw new Exception("This critical dependence was not taken into account." +
					"Critical dependencies must be added before initialization begins.");
			}
                
			if (_criticalSystems.Count != 0)
			{
				return;
			}
			
			OnCriticalSystemsInitialized?.Invoke();
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