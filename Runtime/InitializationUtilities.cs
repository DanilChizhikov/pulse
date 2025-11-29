using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DTech.Pulse
{
	internal static class InitializationUtilities
	{
		public static Type[] GetDependencies(this object system)
		{
			var result = new HashSet<Type>();
			MemberInfo[] members = system.GetType().GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
			foreach (MemberInfo member in members)
			{
				var attribute = member.GetCustomAttribute<InitDependencyAttribute>();
				if (attribute != null)
				{
					switch (member)
					{
						case FieldInfo fieldInfo:
						{
							result.Add(fieldInfo.FieldType);
						} break;

						case MethodInfo methodInfo:
						{
							ParameterInfo[] parameters = methodInfo.GetParameters();
							foreach (ParameterInfo parameter in parameters)
							{
								result.Add(parameter.ParameterType);
							}
						} break;

						case PropertyInfo propertyInfo:
						{
							result.Add(propertyInfo.PropertyType);
						} break;
					}
				}
			}

			ConstructorInfo[] constructors = system.GetType().GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			ConstructorInfo constructorInfo = null;
			if (constructors.Length > 1)
			{
				constructorInfo = GetConstructor(constructors);
			}
			else if (constructors.Length == 1)
			{
				constructorInfo = constructors[0];
			}

			if (constructorInfo != null)
			{
				result.UnionWith(GetDependencies(constructorInfo));
			}
			
			result.RemoveWhere(type => !typeof(IInitializable).IsAssignableFrom(type));
			return result.ToArray();
		}
		
		private static Type[] GetDependencies(ConstructorInfo constructor)
		{
			ParameterInfo[] parameterInfos = constructor.GetParameters();
			return parameterInfos.Select(parameterInfo => parameterInfo.ParameterType).ToArray();
		}
		
		private static ConstructorInfo GetConstructor(IReadOnlyList<ConstructorInfo> constructors)
		{
			ConstructorInfo publicConstructor = null;
			bool hasPublicConstructor = false;
			for (int i = 0; i < constructors.Count; i++)
			{
				ConstructorInfo constructorInfo = constructors[i];
				var attribute = constructorInfo.GetCustomAttribute<InitDependencyAttribute>();
				if (attribute != null)
				{
					return constructorInfo;
				}

				if (constructorInfo.IsPublic && !hasPublicConstructor)
				{
					publicConstructor = constructorInfo;
					hasPublicConstructor = true;
				}
			}

			return publicConstructor;
		}
	}
}