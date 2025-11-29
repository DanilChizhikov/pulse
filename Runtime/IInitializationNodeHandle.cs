using System;

namespace DTech.Pulse
{
	public interface IInitializationNodeHandle
	{
		Type SystemType { get; }
		
		IInitializationNodeHandle AddDependency<T>() where T : IInitializable;
		IInitializationNodeHandle AddDependencies(params Type[] dependencies);
		IInitializationNodeHandle RemoveDependency<T>() where T : IInitializable;
		IInitializationNodeHandle RemoveDependencies(params Type[] dependencies);
		IInitializationNodeHandle SetAsCritical();
		IInitializationNodeHandle OnStartInitialize(Action<Type> callback);
		IInitializationNodeHandle OnCompleteInitialize(Action<Type> callback);
	}
}