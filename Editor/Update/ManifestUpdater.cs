using System;
using System.IO;
using System.Text.RegularExpressions;

namespace DTech.Pulse.Editor.Update
{
    internal static class ManifestUpdater
    {
        public const string GitUrl = "https://github.com/DanilChizhikov/pulse.git";
        public const string LocalPackageError = "Package '" + PackageName + "' is not found in manifest.";
        
        private const string PackageName = "com.dtech.pulse";
        private const string ManifestPath = "Packages/manifest.json";

        public static bool TryUpdateManifest(Version version, out string error)
        {
            error = string.Empty;
            if (!File.Exists(ManifestPath))
            {
                error = $"Manifest not found: {ManifestPath}";
                return false;
            }

            try
            {
                string manifest = File.ReadAllText(ManifestPath);
                var pattern = $@"(""{Regex.Escape(PackageName)}""\s*:\s*"")(.*?)("")";
                var match = Regex.Match(manifest, pattern);
                if (!match.Success)
                {
                    error = LocalPackageError;
                    return false;
                }

                string currentSource = match.Groups[2].Value;

                string newSource = IsGitSource(currentSource)
                    ? $"{GitUrl}#v{version}"
                    : version.ToString();

                manifest = manifest[..match.Index]
                    + match.Groups[1].Value
                    + newSource
                    + match.Groups[3].Value
                    + manifest[(match.Index + match.Length)..];

                File.WriteAllText(ManifestPath, manifest);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public static bool IsGitSource(string source)
        {
            return source.Contains(".git", StringComparison.OrdinalIgnoreCase)
                   || source.StartsWith("git+", StringComparison.OrdinalIgnoreCase);
        }
    }
}