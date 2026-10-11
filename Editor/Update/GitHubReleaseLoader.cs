using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace DTech.Pulse.Editor.Update
{
    internal static class GitHubReleaseLoader
    {
        private const string ApiUrl = "https://api.github.com/repos/{0}/{1}/releases/tags/{2}";
        private const string FullChangelogPrefix = "**Full Changelog**";
        private const string EmptyBody = "_No description._";

        public static void Load(string owner, string repository, string version, VisualElement container)
        {
            string url = string.Format(ApiUrl, owner, repository, version);

            var request = UnityWebRequest.Get(url);

            request.SetRequestHeader("Accept", "application/vnd.github+json");

            request.SetRequestHeader("User-Agent", "Unity-Editor-Package-Updater");

            var operation = request.SendWebRequest();

            EditorApplication.update += WaitForRequest;

            void WaitForRequest()
            {
                if (!operation.isDone)
                {
                    return;
                }

                EditorApplication.update -= WaitForRequest;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Failed to load GitHub release: {request.error}");

                    container.Add(new Label($"Failed to load changelog: {request.error}"));

                    request.Dispose();
                    return;
                }

                try
                {
                    var release = JsonUtility.FromJson<GitHubRelease>(request.downloadHandler.text);

                    BuildChangelog(container, release.body);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);

                    container.Add(new Label("Failed to parse changelog."));
                }
                finally
                {
                    request.Dispose();
                }
            }
        }

        private static void BuildChangelog(VisualElement container, string markdown)
        {
            container.Clear();

            string body = RemoveFullChangelog(markdown);
            var view = new ReleaseNotesView();
            view.SetText(string.IsNullOrWhiteSpace(body) ? EmptyBody : body);
            container.Add(view);
        }

        private static string RemoveFullChangelog(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
            {
                return string.Empty;
            }

            string[] lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var builder = new StringBuilder(markdown.Length);
            foreach (string line in lines)
            {
                if (line.TrimStart().StartsWith(FullChangelogPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                builder.Append(line).Append('\n');
            }

            return builder.ToString().Trim();
        }

        [Serializable]
        private sealed class GitHubRelease
        {
            public string tag_name;
            public string name;
            public string body;
        }
    }
}