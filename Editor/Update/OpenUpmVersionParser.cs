using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace DTech.Pulse.Editor.Update
{
    internal sealed class OpenUpmVersionParser : VersionParser
    {
        private const string RegistryUrl = "https://package.openupm.com/";
        private const string PackageName = "com.dtech.pulse";

        public override bool TryGetLastVersion(out Version version, out string error)
        {
            version = new Version(0, 0, 0);
            error = string.Empty;

            using UnityWebRequest request = UnityWebRequest.Get($"{RegistryUrl}{PackageName}");
            var operation = request.SendWebRequest();

            while (!operation.isDone)
            {
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                error = request.error;
                return false;
            }

            try
            {
                var metadata = JsonUtility.FromJson<PackageMetadata>(request.downloadHandler.text);
                if (metadata.Versions == null)
                {
                    error = "No versions found in OpenUPM metadata.";
                    return false;
                }

                Version latestVersion = metadata.Versions
                    .Select(x => ParseVersion(x.Name))
                    .Where(x => x != null)
                    .OrderByDescending(x => x)
                    .FirstOrDefault();

                if (latestVersion == null)
                {
                    error = "No valid versions found in OpenUPM.";
                    return false;
                }

                version = latestVersion;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static Version ParseVersion(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            return Version.TryParse(value, out Version version)
                ? version
                : null;
        }

        [Serializable]
        private sealed class PackageMetadata
        {
            public PackageVersion[] Versions;
        }

        [Serializable]
        private sealed class PackageVersion
        {
            public string Name;
        }
    }
}