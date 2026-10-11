using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.Pulse.Editor.Update
{
	internal sealed class UpdateWindow : EditorWindow
	{
		private const float WindowWidth = 700f;
		private const float WindowHeight = 600f;
		private const string UpdateWindowShownKey = "DTech.Pulse.UpdateWindow.Shown";
		
		private static bool WasShownThisSession
		{
			get => SessionState.GetBool(UpdateWindowShownKey, false);
			set => SessionState.SetBool(UpdateWindowShownKey, value);
		}

		private Version _currentVersion;
		private Version _lastVersion;

		[MenuItem("Tools/DTech/Pulse/Check Update")]
		private static void ForceCheckUpdate()
		{
			if (!VersionUpdateChecker.TryGetCurrentVersion(out Version currentVersion))
			{
				Debug.Log("Pulse update check works only for a package installed through Package Manager");
				return;
			}

			if (VersionUpdateChecker.NeedUpdate(currentVersion, true))
			{
				WasShownThisSession = false;
				ShowWindow();
			}
			else
			{
				Debug.Log($"Pulse already up to actual version '{currentVersion}'");
			}
		}

		[InitializeOnLoadMethod]
		private static void CheckUpdate()
		{
			if (!VersionUpdateChecker.TryGetCurrentVersion(out Version currentVersion) ||
				!VersionUpdateChecker.NeedUpdate(currentVersion, false))
			{
				return;
			}
			
			ShowWindow();
		}
		
		[MenuItem("Tools/PULSE")]
		private static void ShowWindow()
		{
			// if (WasShownThisSession)
			// {
			// 	return;
			// }
			
			var window = GetWindow<UpdateWindow>("Pulse Update");
			window.minSize = new Vector2(WindowWidth, WindowHeight);
			window.maxSize = new Vector2(WindowWidth, WindowHeight);
			window.Show();
			WasShownThisSession = true;
		}

		private void CreateGUI()
		{
			rootVisualElement.Clear();
			VersionUpdateChecker.TryGetCurrentVersion(out _currentVersion);
			_lastVersion = VersionUpdateChecker.GetLastVersion();

			rootVisualElement.style.paddingLeft = 20;
			rootVisualElement.style.paddingRight = 20;
			rootVisualElement.style.paddingTop = 20;
			rootVisualElement.style.paddingBottom = 20;
			
			CreateHeader();
			CreateDescription();
			CreateChangelog();
			CreateFooter();
		}
		
		private void CreateHeader()
		{
			var titleElement = new VisualElement
			{
				style =
				{
					flexDirection = FlexDirection.Row,
					justifyContent = Justify.Center,
					alignItems = Align.Center,
					flexShrink = 0,
				}
			};

			var pulseIcon = new PulseIcon
			{
				style =
				{
					width = 52,
					marginRight = 8,
					alignSelf = Align.Stretch,
				}
			};

			var label = new Label("Pulse")
			{
				style =
				{
					fontSize = 44,
					unityFontStyleAndWeight = FontStyle.Bold,
					color = Color.white
				}
			};

			titleElement.Add(pulseIcon);
			titleElement.Add(label);

			rootVisualElement.Add(titleElement);
		}
		
		private void CreateDescription()
		{
			var descriptionElement = new VisualElement
			{
				style =
				{
					flexDirection = FlexDirection.Column,
					justifyContent = Justify.Center,
					paddingTop = 32,
					paddingBottom = 22,
					flexShrink = 0,
				}
			};

			var topDescriptionLabel = new Label("New version available to update!")
			{
				style =
				{
					fontSize = 18,
					alignSelf = Align.Center,
				}
			};
			
			var currentVersionLabel = new Label($"Current version: {_currentVersion}")
			{
				style =
				{
					paddingTop = 6,
					fontSize = 13,
					unityFontStyleAndWeight = FontStyle.Normal,
					color = Color.white,
				}
			};
			
			var lastVersionLabel = new Label($"New version: {_lastVersion}")
			{
				style =
				{
					fontSize = 13,
					unityFontStyleAndWeight = FontStyle.Normal,
					color = Color.white,
				}
			};
			
			descriptionElement.Add(topDescriptionLabel);
			descriptionElement.Add(currentVersionLabel);
			descriptionElement.Add(lastVersionLabel);

			rootVisualElement.Add(descriptionElement);
		}
		
		private void CreateChangelog()
		{
			var changelogTitle = new Label("What's New")
			{
				style =
				{
					fontSize = 15,
					unityFontStyleAndWeight = FontStyle.Bold,
					marginBottom = 6,
					flexShrink = 0,
				}
			};

			var scrollView = new ScrollView(ScrollViewMode.Vertical)
			{
				style =
				{
					flexGrow = 1,
					minHeight = 0,
				},
				horizontalScrollerVisibility = ScrollerVisibility.Hidden,
			};

			NoteStyles.ApplyFrame(scrollView);

			GitHubReleaseLoader.Load("DanilChizhikov", "pulse", $"v{_lastVersion}", scrollView);
			
			rootVisualElement.Add(changelogTitle);
			rootVisualElement.Add(scrollView);
		}

		private void CreateFooter()
		{
			var footerElement = new VisualElement
			{
				style =
				{
					flexDirection = FlexDirection.Column,
					justifyContent = Justify.Center,
					paddingTop = 22,
					flexShrink = 0,
				}
			};

			var skipButtonsElement = new VisualElement
			{
				style =
				{
					flexDirection = FlexDirection.Row,
					flexShrink = 0f,
				}
			};

			var skipVersionButton = new Button(SkipVersionClickHandler)
			{
				style =
				{
					backgroundColor = new Color(1f, 0.5f, 0.3f),
					flexGrow = 1,
					height = 32,
				},
				text = "Skip Version",
			};

			var skipAlwaysButton = new Button(SkipAlwaysClickHandler)
			{
				style =
				{
					backgroundColor = new Color(1f, 0.3f, 0.3f),
					flexGrow = 1,
					height = 32,
				},
				text = "Skip Always",
			};

			var updateButton = new Button(UpdateClickHandler)
			{
				style =
				{
					backgroundColor = new Color(0.4f, 0.6f, 0.3f),
					height = 44,
				},
				text = "Update",
			};
			
			skipButtonsElement.Add(skipVersionButton);
			skipButtonsElement.Add(skipAlwaysButton);
			footerElement.Add(skipButtonsElement);
			footerElement.Add(updateButton);
			
			rootVisualElement.Add(footerElement);
		}

		private void SkipVersionClickHandler()
		{
			VersionUpdateChecker.SkipVersion();
			Close();
		}
		
		private void SkipAlwaysClickHandler()
		{
			VersionUpdateChecker.SkipAlways();
			Close();
		}

		private void UpdateClickHandler()
		{
			if (ManifestUpdater.TryUpdateManifest(_lastVersion, out string error))
			{
				AssetDatabase.Refresh();
			}
			else if (error == ManifestUpdater.LocalPackageError)
			{
				Application.OpenURL(ManifestUpdater.GitUrl);
			}
			else
			{
				Debug.LogError(error);
			}
			
			Close();
		}
	}
}