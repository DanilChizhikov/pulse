using System;

namespace DTech.Pulse
{
	public interface IInitializationNodeHandle
	{
		Type SystemType { get; }
		IInitializationNodeHandle AddDependency<T>() where T : IInitializable;
		IInitializationNodeHandle AddDependencies(params Type[] dependencies);
		IInitializationNodeHandle SetCritical();
		IInitializationNodeHandle OnStartInitialize(Action<Type> callback);
		IInitializationNodeHandle OnCompleteInitialize(Action<Type> callback);
	}
}