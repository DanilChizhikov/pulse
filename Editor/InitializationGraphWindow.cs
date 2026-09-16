using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.Pulse.Editor
{
	internal sealed class InitializationGraphWindow : EditorWindow
	{
		private const string WindowTitle = "Initialization Graph";
		private const string DefaultSnapshotsMenuText = "Snapshots";

		[SerializeField] private string _selectedPath;
		[SerializeField] private bool _isShowingAllEdges;

		private InitializationGraphView _graphView;
		private ToolbarMenu _snapshotsMenu;
		private ToolbarToggle _recordToggle;
		private Label _summaryLabel;

		[MenuItem("Window/DTech/Pulse/Initialization Graph")]
		public static void ShowWindow()
		{
			var window = GetWindow<InitializationGraphWindow>();
			window.titleContent = new GUIContent(WindowTitle);
			window.Show();
		}
		
		private static string GetLatestSnapshotPath()
		{
			IReadOnlyList<string> paths = InitializationGraphStorage.GetSnapshotPaths();
			return paths.Count > 0 ? paths[0] : null;
		}
		
		private static string GetRecordCaption() => InitializationGraphRecordingMenu.IsRecordingEnabled ? "Stop Recording" : "Start Recording";

		private void OnEnable()
		{
			InitializationGraphStorage.OnSaved += SnapshotSavedHandler;
		}

		private void OnDisable()
		{
			InitializationGraphStorage.OnSaved -= SnapshotSavedHandler;
		}

		private void OnFocus()
		{
			_recordToggle?.SetValueWithoutNotify(InitializationGraphRecordingMenu.IsRecordingEnabled);
		}

		private void CreateGUI()
		{
			rootVisualElement.Add(CreateToolbar());

			_graphView = new InitializationGraphView();
			_graphView.style.flexGrow = 1f;
			_graphView.IsShowingAllEdges = _isShowingAllEdges;
			rootVisualElement.Add(_graphView);

			RefreshSnapshotsMenu();
			LoadSnapshot(File.Exists(_selectedPath) ? _selectedPath : GetLatestSnapshotPath());
		}

		private Toolbar CreateToolbar()
		{
			var toolbar = new Toolbar();

			_snapshotsMenu = new ToolbarMenu { text = DefaultSnapshotsMenuText };
			toolbar.Add(_snapshotsMenu);
			toolbar.Add(new ToolbarButton(RefreshButtonClickHandler) { text = "Refresh" });
			toolbar.Add(new ToolbarButton(OpenFileButtonClickHandler) { text = "Open File..." });
			toolbar.Add(new ToolbarButton(RevealButtonClickHandler) { text = "Reveal" });
			toolbar.Add(new ToolbarButton(FrameAllButtonClickHandler) { text = "Frame All" });

			var allEdgesToggle = new ToolbarToggle
			{
				text = "Transitive edges",
				tooltip = "Also draw edges implied by other dependencies while nothing is selected.",
				value = _isShowingAllEdges,
			};
			allEdgesToggle.RegisterValueChangedCallback(AllEdgesToggleChangedHandler);
			toolbar.Add(allEdgesToggle);

			var spacer = new VisualElement();
			spacer.style.flexGrow = 1f;
			toolbar.Add(spacer);

			_summaryLabel = new Label();
			_summaryLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
			_summaryLabel.style.marginRight = 8f;
			toolbar.Add(_summaryLabel);

			_recordToggle = new ToolbarToggle
			{
				text = GetRecordCaption(),
				value = InitializationGraphRecordingMenu.IsRecordingEnabled,
			};
			_recordToggle.RegisterValueChangedCallback(RecordToggleChangedHandler);
			toolbar.Add(_recordToggle);

			return toolbar;
		}

		private void RefreshSnapshotsMenu()
		{
			DropdownMenu menu = _snapshotsMenu.menu;
			menu.MenuItems().Clear();

			IReadOnlyList<string> paths = InitializationGraphStorage.GetSnapshotPaths();
			if (paths.Count == 0)
			{
				menu.AppendAction("No saved snapshots", _ => { }, DropdownMenuAction.Status.Disabled);
				return;
			}

			foreach (string path in paths)
			{
				menu.AppendAction(
					Path.GetFileNameWithoutExtension(path),
					_ => LoadSnapshot(path),
					_ => path == _selectedPath ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
			}
		}

		private void LoadSnapshot(string path)
		{
			_selectedPath = path;
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
			{
				ShowEmpty("No snapshot. Enable Record and enter Play Mode.");
				return;
			}

			InitializationGraphSnapshot snapshot;
			try
			{
				snapshot = InitializationGraphStorage.Load(path);
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				ShowEmpty($"Failed to load '{Path.GetFileName(path)}'.");
				return;
			}

			_snapshotsMenu.text = Path.GetFileNameWithoutExtension(path);
			int levelsCount = InitializationGraphLevels.GetCount(InitializationGraphLevels.Calculate(snapshot));
			_summaryLabel.text = $"{snapshot.Status} · {InitializationTimeFormat.Format(snapshot.TotalMilliseconds)} · " +
				$"{snapshot.Systems.Count} systems · {levelsCount} levels";
			_graphView.Show(snapshot);
		}

		private void ShowEmpty(string message)
		{
			_snapshotsMenu.text = DefaultSnapshotsMenuText;
			_summaryLabel.text = message;
			_graphView.Show(null);
		}

		private void SnapshotSavedHandler(string path)
		{
			if (_graphView == null)
			{
				_selectedPath = path;
				return;
			}

			RefreshSnapshotsMenu();
			LoadSnapshot(path);
		}

		private void RefreshButtonClickHandler()
		{
			RefreshSnapshotsMenu();
			LoadSnapshot(File.Exists(_selectedPath) ? _selectedPath : GetLatestSnapshotPath());
		}

		private void OpenFileButtonClickHandler()
		{
			string path = EditorUtility.OpenFilePanel("Open Initialization Graph", InitializationGraphStorage.DirectoryPath, "xml");
			if (!string.IsNullOrEmpty(path))
			{
				LoadSnapshot(path);
			}
		}

		private void RevealButtonClickHandler()
		{
			if (File.Exists(_selectedPath))
			{
				EditorUtility.RevealInFinder(_selectedPath);
				return;
			}

			string directoryPath = InitializationGraphStorage.DirectoryPath;
			if (Directory.Exists(directoryPath))
			{
				EditorUtility.RevealInFinder(directoryPath);
			}
		}

		private void FrameAllButtonClickHandler()
		{
			_graphView.FrameAll();
		}

		private void RecordToggleChangedHandler(ChangeEvent<bool> changeEvent)
		{
			InitializationGraphRecordingMenu.IsRecordingEnabled = changeEvent.newValue;
			_recordToggle.text = GetRecordCaption();
		}

		private void AllEdgesToggleChangedHandler(ChangeEvent<bool> changeEvent)
		{
			_isShowingAllEdges = changeEvent.newValue;
			_graphView.IsShowingAllEdges = changeEvent.newValue;
		}
	}
}
