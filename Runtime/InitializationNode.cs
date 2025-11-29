using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DTech.Pulse
{
	internal sealed class InitializationNode : IInitializationNodeHandle
	{
		private event Action<Type> OnInitializeStarted;
		private event Action<Type> OnInitializeCompleted;
		
		private readonly IInitializable _system;
		private readonly HashSet<Type> _dependencies;
		private readonly HashSet<Type> _removedDependencies;
		
		public Type SystemType { get; }

		public bool IsCritical { get; private set; }
		
		private bool _isProcessed;

		internal InitializationNode(IInitializable system)
		{
			_system = system ?? throw new ArgumentNullException(nameof(system));
			SystemType = _system.GetType();
			IsCritical = false;
			_dependencies = new HashSet<Type>();
			_removedDependencies = new HashSet<Type>();
		}
		
		public IInitializationNodeHandle AddDependency<T>()
			where T : IInitializable
		{
			return AddDependencies(typeof(T));
		}

		public IInitializationNodeHandle AddDependencies(params Type[] dependencies)
		{
			if (_isProcessed)
			{
				throw new Exception("This node has already been validated. You must add dependencies before initialization begins.");
			}
			
			foreach (Type dependency in dependencies)
			{
				if (!typeof(IInitializable).IsAssignableFrom(dependency))
				{
					throw new Exception($"Dependency {dependency} is not an instance of {typeof(IInitializable)}");
				}
				
				_dependencies.Add(dependency);
			}
			
			return this;
		}

		public IInitializationNodeHandle RemoveDependency<T>()
			where T : IInitializable
		{
			return RemoveDependencies(typeof(T));
		}

		public IInitializationNodeHandle RemoveDependencies(params Type[] dependencies)
		{
			if (_isProcessed)
			{
				throw new Exception("This node has already been validated. You must remove dependencies before initialization begins.");
			}
			
			foreach (Type dependency in dependencies)
			{
				_removedDependencies.Add(dependency);
			}
			
			return this;
		}

		public IInitializationNodeHandle SetAsCritical()
		{
			if (_isProcessed)
			{
				throw new Exception("This node has already been validated. You must set criticality before initialization begins.");
			}
			
			IsCritical = true;
			return this;
		}

		public IInitializationNodeHandle OnStartInitialize(Action<Type> callback)
		{
			OnInitializeStarted += callback;
			return this;
		}

		public IInitializationNodeHandle OnCompleteInitialize(Action<Type> callback)
		{
			OnInitializeCompleted += callback;
			return this;
		}

		public List<Type> GetDependencies()
		{
			if (_isProcessed)
			{
				throw new Exception("This node has already been validated. You cannot retrieve dependencies after initialization begins.");
			}
			
			var result = new List<Type>(_dependencies);
			var dependenciesToRemove = new HashSet<Type>();
			foreach (Type removableDependency in _removedDependencies)
			{
				foreach (Type dependency in _dependencies)
				{
					if (removableDependency.IsAssignableFrom(dependency))
					{
						dependenciesToRemove.Add(dependency);
					}
				}
			}
			
			result.RemoveAll(dependenciesToRemove.Contains);
			return result;
		}

		internal async Task InitializeAsync(CancellationToken cancellationToken)
		{
			OnInitializeStarted?.Invoke(SystemType);
			await _system.InitializeAsync(cancellationToken);
			OnInitializeCompleted?.Invoke(SystemType);
			OnInitializeStarted = null;
			OnInitializeCompleted = null;
		}
		
		internal void SetProcessed() => _isProcessed = true;
	}
}