using System;
using UnityEngine.Scripting;

namespace DTech.Pulse
{
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Constructor)]
	[Preserve]
	public sealed class InitDependencyAttribute : Attribute
	{
	}
}
