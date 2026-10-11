using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace DTech.Pulse.Editor.Update
{
    internal sealed class GitVersionParser : VersionParser
    {
        private const string TagsUrl = "https://api.github.com/repos/DanilChizhikov/pulse/tags?per_page=100";

        public override bool TryGetLastVersion(out Version version, out string error)
        {
            version = new Version(0, 0, 0);
            error = string.Empty;

            using UnityWebRequest request = UnityWebRequest.Get(TagsUrl);

            request.SetRequestHeader("Accept", "application/vnd.github+json");
            request.SetRequestHeader("User-Agent", "DTech.Pulse");

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
                var tags = JsonUtility.FromJson<GitTagsResponse>($"{{\"items\":{request.downloadHandler.text}}}");

                Version latestVersion = tags.items
                    .Select(x => ParseVersion(x.name))
                    .Where(x => x != null)
                    .OrderByDescending(x => x)
                    .FirstOrDefault();

                if (latestVersion == null)
                {
                    error = "No valid versions found in Git tags.";
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

        private static Version ParseVersion(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return null;
            }

            string value = tag.Trim();

            if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(1);
            }

            return Version.TryParse(value, out Version version)
                ? version
                : null;
        }

        [Serializable]
        private sealed class GitTagsResponse
        {
            public GitTag[] items;
        }

        [Serializable]
        private sealed class GitTag
        {
            public string name;
        }
    }
}