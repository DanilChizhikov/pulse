using System;
using System.IO;
using UnityEngine;

namespace DTech.Pulse.Editor
{
	internal static class InitializationGraphStorage
	{
		public static string DirectoryPath => Directory.GetParent(Application.dataPath).FullName;

		public static void Export(InitializationGraphSnapshot snapshot, string path)
		{
			if (snapshot == null)
			{
				throw new ArgumentNullException(nameof(snapshot));
			}

			if (string.IsNullOrEmpty(path))
			{
				throw new ArgumentException("Path cannot be null or empty.", nameof(path));
			}

			File.WriteAllText(path, snapshot.ToXml(true));
		}

		public static InitializationGraphSnapshot Load(string path)
		{
			return InitializationGraphSnapshot.FromXml(File.ReadAllText(path));
		}
	}
}