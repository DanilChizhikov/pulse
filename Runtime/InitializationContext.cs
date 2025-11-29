using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DTech.Pulse
{
	public sealed class InitializationContext
	{
		public event Action OnCriticalSystemsInitialized;
		
		private readonly List<ICollection<InitializationNode>> _batches;
		private readonly HashSet<InitializationNode> _criticalSystems;
		
		internal InitializationContext(List<ICollection<InitializationNode>> batches, IEnumerable<InitializationNode> criticalSystems)
		{
			_batches = batches;
			_criticalSystems = new HashSet<InitializationNode>(criticalSystems);
		}

		public async Task InitializationAsync(CancellationToken token)
		{
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
	}
}