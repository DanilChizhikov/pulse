using System;
using UnityEngine.LowLevel;
using UnityEngine.Scripting;

namespace DTech.Pulse
{
	[Preserve]
	internal static class PlayerLoopUtilities
	{
		public static bool TryAppendSubSystem(ref PlayerLoopSystem root, Type parentType, PlayerLoopSystem system)
		{
			PlayerLoopSystem[] subSystems = root.subSystemList;
			if (subSystems == null)
			{
				return false;
			}

			var copy = new PlayerLoopSystem[subSystems.Length];
			Array.Copy(subSystems, copy, subSystems.Length);

			for (int i = 0; i < copy.Length; i++)
			{
				if (copy[i].type == parentType)
				{
					PlayerLoopSystem[] children = copy[i].subSystemList;
					int childrenCount = children?.Length ?? 0;
					var newChildren = new PlayerLoopSystem[childrenCount + 1];
					if (childrenCount > 0)
					{
						Array.Copy(children, newChildren, childrenCount);
					}

					newChildren[childrenCount] = system;
					copy[i].subSystemList = newChildren;
					root.subSystemList = copy;
					return true;
				}

				PlayerLoopSystem child = copy[i];
				if (!TryAppendSubSystem(ref child, parentType, system))
				{
					continue;
				}

				copy[i] = child;
				root.subSystemList = copy;
				return true;
			}

			return false;
		}
	}
}