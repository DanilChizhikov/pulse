using System;
using UnityEngine.Scripting;

namespace DTech.Pulse
{
	/// <summary>
	/// Configuration handle of a system registered in <see cref="InitializationContextBuilder"/>.
	/// </summary>
	/// <remarks>
	/// All methods must be called before <see cref="InitializationContextBuilder.Build"/>;
	/// afterwards the node is sealed and any modification throws <see cref="InvalidOperationException"/>.
	/// </remarks>
	[Preserve]
	public interface IInitializationNodeHandle
	{
		/// <summary>
		/// Concrete type of the system represented by this node.
		/// </summary>
		Type SystemType { get; }
		
		/// <summary>
		/// Adds a dependency on another system, which will be initialized before this one.
		/// </summary>
		/// <typeparam name="T">Type of the system this node depends on.</typeparam>
		/// <returns>The same handle, allowing calls to be chained.</returns>
		IInitializationNodeHandle AddDependency<T>() where T : IInitializable;

		/// <summary>
		/// Adds several dependencies at once, all of which will be initialized before this system.
		/// </summary>
		/// <param name="dependencies">Types implementing <see cref="IInitializable"/>.</param>
		/// <returns>The same handle, allowing calls to be chained.</returns>
		IInitializationNodeHandle AddDependencies(params Type[] dependencies);

		/// <summary>
		/// Removes a previously declared dependency, including the ones discovered through
		/// <see cref="InitDependencyAttribute"/>.
		/// </summary>
		/// <typeparam name="T">Type of the dependency to remove. Types assignable from it are removed as well.</typeparam>
		/// <returns>The same handle, allowing calls to be chained.</returns>
		IInitializationNodeHandle RemoveDependency<T>() where T : IInitializable;

		/// <summary>
		/// Removes several previously declared dependencies at once.
		/// </summary>
		/// <param name="dependencies">Types of the dependencies to remove. Types assignable from them are removed as well.</param>
		/// <returns>The same handle, allowing calls to be chained.</returns>
		IInitializationNodeHandle RemoveDependencies(params Type[] dependencies);

		/// <summary>
		/// Marks the system as critical, so it is counted by
		/// <see cref="InitializationContext.OnCriticalSystemsInitialized"/>.
		/// </summary>
		/// <returns>The same handle, allowing calls to be chained.</returns>
		IInitializationNodeHandle SetAsCritical();

		/// <summary>
		/// Subscribes a callback invoked right before this system starts initializing.
		/// </summary>
		/// <param name="callback">Callback receiving the system type.</param>
		/// <returns>The same handle, allowing calls to be chained.</returns>
		IInitializationNodeHandle OnStartInitialize(Action<Type> callback);

		/// <summary>
		/// Subscribes a callback invoked right after this system finishes initializing.
		/// </summary>
		/// <param name="callback">Callback receiving the system type.</param>
		/// <returns>The same handle, allowing calls to be chained.</returns>
		IInitializationNodeHandle OnCompleteInitialize(Action<Type> callback);
	}
}
