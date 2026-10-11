using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace DTech.Pulse.Editor.Update
{
	internal static class VersionUpdateChecker
	{
		private const string PrefsKey = "DTech.Pulse.VersionUpdateChecker";
		private const string ManifestPath = "Packages/manifest.json";
		private const string PackageName = "com.dtech.pulse";

		private static readonly VersionUpdateSave _defaultSave = new VersionUpdateSave
		{
			IsSkipThisVersion = false,
			IsSkipAlways = false,
			Version = "0.0.0",
		};

		public static bool TryGetCurrentVersion(out Version version)
		{
			UnityEditor.PackageManager.PackageInfo info =
				UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(VersionUpdateChecker).Assembly);

			version = null;
			return info != null && Version.TryParse(info.version, out version);
		}

		public static Version GetLastVersion()
		{
			VersionParser parser = GetVersionParser();
			if (!parser.TryGetLastVersion(out Version version, out string error))
			{
				throw new InvalidOperationException(error);
			}

			return version;
		}
		
		public static bool NeedUpdate(Version version, bool force)
		{
			string defaultSaveJson = JsonUtility.ToJson(_defaultSave);
			string savedJson = EditorPrefs.GetString(PrefsKey, defaultSaveJson);
			var versionUpdateSave = JsonUtility.FromJson<VersionUpdateSave>(savedJson);
			var savedVersion = new Version(versionUpdateSave.Version);
			if ((versionUpdateSave.IsSkipAlways || (versionUpdateSave.IsSkipThisVersion && savedVersion >= version)) && !force)
			{
				return false;
			}

			Version lastVersion = GetLastVersion();
			if (lastVersion <= version)
			{
				return false;
			}
			
			return true;
		}
		
		public static void SkipVersion()
		{
			string defaultSaveJson = JsonUtility.ToJson(_defaultSave);
			string savedJson = EditorPrefs.GetString(PrefsKey, defaultSaveJson);
			var versionUpdateSave = JsonUtility.FromJson<VersionUpdateSave>(savedJson);
			versionUpdateSave.IsSkipThisVersion = true;
			versionUpdateSave.Version = GetLastVersion().ToString();
			EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(versionUpdateSave));
		}
		
		public static void SkipAlways()
		{
			string defaultSaveJson = JsonUtility.ToJson(_defaultSave);
			string savedJson = EditorPrefs.GetString(PrefsKey, defaultSaveJson);
			var versionUpdateSave = JsonUtility.FromJson<VersionUpdateSave>(savedJson);
			versionUpdateSave.IsSkipAlways = true;
			EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(versionUpdateSave));
		}
		
		private static VersionParser GetVersionParser()
		{
			if (!File.Exists(ManifestPath))
			{
				return new GitVersionParser();
			}

			string manifest = File.ReadAllText(ManifestPath);
			var pattern = $@"(""{Regex.Escape(PackageName)}""\s*:\s*"")(.*?)("")";
			var match = Regex.Match(manifest, pattern);
			if (!match.Success)
			{
				return new GitVersionParser();
			}

			string source = match.Groups[2].Value;

			return ManifestUpdater.IsGitSource(source)
				? new GitVersionParser()
				: new OpenUpmVersionParser();
		}

		private static VersionParser[] GetParsers()
		{
			Type[] types = TypeCache.GetTypesDerivedFrom<VersionParser>().Where(t => !t.IsAbstract).ToArray();
			return types.Select(t => (VersionParser)Activator.CreateInstance(t)).ToArray();
		}
		
		[Serializable]
		private sealed class VersionUpdateSave
		{
			public bool IsSkipThisVersion;
			public bool IsSkipAlways;
			public string Version;
		}
	}
}