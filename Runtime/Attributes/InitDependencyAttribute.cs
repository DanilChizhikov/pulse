using System;
using UnityEngine.Scripting;

namespace DTech.Pulse
{
	/// <summary>
	/// Marks a field, property, method or constructor whose <see cref="IInitializable"/> types
	/// must be initialized before the system that declares them.
	/// </summary>
	/// <remarks>
	/// When a system has several constructors, exactly one of them has to be marked with this attribute.
	/// </remarks>
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Constructor)]
	[Preserve]
	public sealed class InitDependencyAttribute : Attribute
	{
	}
}
