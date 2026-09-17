using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DTech.Pulse.Editor
{
	internal static class InitializationGraphStorage
	{
		public static event Action<string> OnSaved;
		
		private const int MaxStoredSnapshots = 20;
		private const string FileExtension = ".xml";

		public static string DirectoryPath =>
			Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", "Pulse", "Graphs");

		public static void Save(InitializationGraphSnapshot snapshot)
		{
			if (snapshot == null)
			{
				throw new ArgumentNullException(nameof(snapshot));
			}

			string directoryPath = DirectoryPath;
			Directory.CreateDirectory(directoryPath);

			string fileName = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
			string path = Path.Combine(directoryPath, fileName + FileExtension);
			for (int suffix = 1; File.Exists(path); suffix++)
			{
				path = Path.Combine(directoryPath, $"{fileName}_{suffix}{FileExtension}");
			}

			File.WriteAllText(path, snapshot.ToXml(true));
			TrimOldSnapshots();
			OnSaved?.Invoke(path);
		}
		
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

		public static IReadOnlyList<string> GetSnapshotPaths()
		{
			string directoryPath = DirectoryPath;
			if (!Directory.Exists(directoryPath))
			{
				return Array.Empty<string>();
			}

			return Directory.GetFiles(directoryPath, "*" + FileExtension)
				.OrderByDescending(path => Path.GetFileName(path), StringComparer.Ordinal)
				.ToArray();
		}

		public static InitializationGraphSnapshot Load(string path)
		{
			return InitializationGraphSnapshot.FromXml(File.ReadAllText(path));
		}

		private static void TrimOldSnapshots()
		{
			IReadOnlyList<string> paths = GetSnapshotPaths();
			for (int i = MaxStoredSnapshots; i < paths.Count; i++)
			{
				File.Delete(paths[i]);
			}
		}
	}
}