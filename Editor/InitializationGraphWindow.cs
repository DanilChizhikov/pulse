using System;
using System.Collections.Generic;
using System.Globalization;
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
		[SerializeField] private GraphSource _source;

		private InitializationGraphView _graphView;
		private ToolbarMenu _sourceMenu;
		private ToolbarMenu _snapshotsMenu;
		private ToolbarMenu _deviceMenu;
		private VisualElement _editorControls;
		private VisualElement _deviceControls;
		private ToolbarButton _saveXmlButton;
		private ToolbarToggle _recordToggle;
		private Label _summaryLabel;

		private DeviceSnapshot _selectedDevice;
		private InitializationGraphSnapshot _currentSnapshot;

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
			InitializationGraphDeviceSource.OnReceived += DeviceSnapshotReceivedHandler;
		}

		private void OnDisable()
		{
			InitializationGraphStorage.OnSaved -= SnapshotSavedHandler;
			InitializationGraphDeviceSource.OnReceived -= DeviceSnapshotReceivedHandler;
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

			ApplySource();
		}

		private Toolbar CreateToolbar()
		{
			var toolbar = new Toolbar();

			_sourceMenu = new ToolbarMenu
			{
				tooltip = "Where the graph is read from: snapshots saved by the Editor or a connected player.",
			};
			_sourceMenu.menu.AppendAction(
				GraphSource.Editor.ToString(),
				_ => SetSource(GraphSource.Editor),
				_ => _source == GraphSource.Editor ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
			_sourceMenu.menu.AppendAction(
				GraphSource.Device.ToString(),
				_ => SetSource(GraphSource.Device),
				_ => _source == GraphSource.Device ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
			toolbar.Add(_sourceMenu);

			_editorControls = new VisualElement();
			_editorControls.style.flexDirection = FlexDirection.Row;
			_snapshotsMenu = new ToolbarMenu { text = DefaultSnapshotsMenuText };
			_editorControls.Add(_snapshotsMenu);
			_editorControls.Add(new ToolbarButton(RefreshButtonClickHandler) { text = "Refresh" });
			_editorControls.Add(new ToolbarButton(OpenFileButtonClickHandler) { text = "Open File..." });
			_editorControls.Add(new ToolbarButton(RevealButtonClickHandler) { text = "Reveal" });
			toolbar.Add(_editorControls);

			_deviceControls = new VisualElement();
			_deviceControls.style.flexDirection = FlexDirection.Row;
			_deviceMenu = new ToolbarMenu { text = DefaultSnapshotsMenuText };
			_deviceControls.Add(_deviceMenu);
			_deviceControls.Add(new ToolbarButton(RequestButtonClickHandler)
			{
				text = "Request",
				tooltip = "Ask connected players to resend their last recorded snapshot.",
			});
			_saveXmlButton = new ToolbarButton(SaveXmlButtonClickHandler) { text = "Save XML..." };
			_deviceControls.Add(_saveXmlButton);
			toolbar.Add(_deviceControls);

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

		private void SetSource(GraphSource source)
		{
			if (_source == source)
			{
				return;
			}

			_source = source;
			ApplySource();
		}

		private void ApplySource()
		{
			bool isDevice = _source == GraphSource.Device;
			_sourceMenu.text = $"Source: {_source}";
			_editorControls.style.display = isDevice ? DisplayStyle.None : DisplayStyle.Flex;
			_deviceControls.style.display = isDevice ? DisplayStyle.Flex : DisplayStyle.None;

			if (isDevice)
			{
				RefreshDeviceMenu();
				LoadDeviceSnapshot(_selectedDevice ?? GetLatestDeviceSnapshot());
				return;
			}

			RefreshSnapshotsMenu();
			LoadSnapshot(File.Exists(_selectedPath) ? _selectedPath : GetLatestSnapshotPath());
		}

		private static DeviceSnapshot GetLatestDeviceSnapshot()
		{
			IReadOnlyList<DeviceSnapshot> snapshots = InitializationGraphDeviceSource.Snapshots;
			return snapshots.Count > 0 ? snapshots[0] : null;
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

		private void RefreshDeviceMenu()
		{
			DropdownMenu menu = _deviceMenu.menu;
			menu.MenuItems().Clear();

			IReadOnlyList<DeviceSnapshot> snapshots = InitializationGraphDeviceSource.Snapshots;
			if (snapshots.Count == 0)
			{
				menu.AppendAction("No received snapshots", _ => { }, DropdownMenuAction.Status.Disabled);
				return;
			}

			foreach (DeviceSnapshot received in snapshots)
			{
				menu.AppendAction(
					received.Label,
					_ => LoadDeviceSnapshot(received),
					_ => received == _selectedDevice ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
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
			ShowSnapshot(snapshot);
		}

		private void LoadDeviceSnapshot(DeviceSnapshot received)
		{
			_selectedDevice = received;
			if (received == null)
			{
				ShowEmpty("No device snapshots. Connect a development build " +
					$"(players: {InitializationGraphDeviceSource.ConnectedPlayersCount.ToString(CultureInfo.InvariantCulture)}).");
				return;
			}

			_deviceMenu.text = received.Label;
			ShowSnapshot(received.Snapshot);
		}

		private void ShowSnapshot(InitializationGraphSnapshot snapshot)
		{
			_currentSnapshot = snapshot;
			_saveXmlButton.SetEnabled(true);

			int levelsCount = InitializationGraphLevels.GetCount(InitializationGraphLevels.Calculate(snapshot));
			_summaryLabel.text = $"{snapshot.Status} · {InitializationTimeFormat.Format(snapshot.TotalMilliseconds)} · " +
				$"{snapshot.Systems.Count} systems · {levelsCount} levels";
			_graphView.Show(snapshot);
		}

		private void ShowEmpty(string message)
		{
			_currentSnapshot = null;
			_saveXmlButton.SetEnabled(false);
			_snapshotsMenu.text = DefaultSnapshotsMenuText;
			_deviceMenu.text = DefaultSnapshotsMenuText;
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

			if (_source != GraphSource.Editor)
			{
				_selectedPath = path;
				return;
			}

			RefreshSnapshotsMenu();
			LoadSnapshot(path);
		}

		private void DeviceSnapshotReceivedHandler(DeviceSnapshot received)
		{
			if (_graphView == null || _source != GraphSource.Device)
			{
				_selectedDevice = received;
				return;
			}

			RefreshDeviceMenu();
			LoadDeviceSnapshot(received);
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

		private void RequestButtonClickHandler()
		{
			int playersCount = InitializationGraphDeviceSource.ConnectedPlayersCount;
			if (playersCount == 0)
			{
				_summaryLabel.text = "No connected players. Run a development build with the profiler attached.";
				return;
			}

			InitializationGraphDeviceSource.RequestSnapshot();
		}

		private void SaveXmlButtonClickHandler()
		{
			if (_currentSnapshot == null)
			{
				return;
			}

			string directoryPath = InitializationGraphStorage.DirectoryPath;
			Directory.CreateDirectory(directoryPath);
			string fileName = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
			string path = EditorUtility.SaveFilePanel("Save Initialization Graph", directoryPath, fileName, "xml");
			if (string.IsNullOrEmpty(path))
			{
				return;
			}

			try
			{
				InitializationGraphStorage.Export(_currentSnapshot, path);
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
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

		private enum GraphSource
		{
			Editor = 0,
			Device = 1,
		}
	}
}