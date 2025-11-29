using System;

namespace DTech.Pulse
{
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Constructor)]
	public sealed class InitDependencyAttribute : Attribute
	{
	}
}