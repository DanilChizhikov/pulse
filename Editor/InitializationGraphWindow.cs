using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.Networking.PlayerConnection;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Networking.PlayerConnection;
using UnityEngine.UIElements;

namespace DTech.Pulse.Editor
{
	internal sealed class InitializationGraphWindow : EditorWindow
	{
		private const string WindowTitle = "Initialization Graph";
		private const string DefaultSnapshotsMenuText = "Snapshots";

		[SerializeField] private string _selectedPath;
		[SerializeField] private bool _isShowingFile;
		[SerializeField] private bool _isShowingAllEdges;
		[SerializeField] private bool _isShowingAllDevices;
		
		private bool IsDeviceSource => _connectionState?.connectedToTarget == ConnectionTarget.Player;

		private InitializationGraphView _graphView;
		private ToolbarMenu _snapshotsMenu;
		private ToolbarMenu _deviceMenu;
		private IMGUIContainer _connectionDropdown;
		private VisualElement _editorControls;
		private VisualElement _deviceControls;
		private ToolbarMenu _exportMenu;
		private Label _summaryLabel;
		private IConnectionState _connectionState;
		private RecordedSnapshot _selectedRecorded;
		private DeviceSnapshot _selectedDevice;
		private InitializationGraphSnapshot _currentSnapshot;

		[MenuItem("Window/DTech/Pulse/Initialization Graph")]
		public static void ShowWindow()
		{
			var window = GetWindow<InitializationGraphWindow>();
			window.titleContent = new GUIContent(WindowTitle);
			window.Show();
		}

		private static RecordedSnapshot GetLatestRecordedSnapshot()
		{
			IReadOnlyList<RecordedSnapshot> snapshots = InitializationGraphHistory.Snapshots;
			return snapshots.Count > 0 ? snapshots[0] : null;
		}

		private static bool IsRecorded(IReadOnlyList<RecordedSnapshot> snapshots, RecordedSnapshot recorded)
		{
			if (recorded == null)
			{
				return false;
			}

			for (int i = 0; i < snapshots.Count; i++)
			{
				if (snapshots[i] == recorded)
				{
					return true;
				}
			}

			return false;
		}

		private void OnEnable()
		{
			InitializationGraphHistory.OnRecorded += RecordedHandler;
			InitializationGraphDeviceSource.OnReceived += DeviceSnapshotReceivedHandler;
			InitializationGraphDeviceSource.OnPlayersChanged += PlayersChangedHandler;
		}

		private void OnDisable()
		{
			InitializationGraphHistory.OnRecorded -= RecordedHandler;
			InitializationGraphDeviceSource.OnReceived -= DeviceSnapshotReceivedHandler;
			InitializationGraphDeviceSource.OnPlayersChanged -= PlayersChangedHandler;
			DisposeConnectionState();
		}

		private void OnDestroy()
		{
			DisposeConnectionState();
		}
		
		private void CreateGUI()
		{
			_connectionState = PlayerConnectionGUIUtility.GetConnectionState(this, ConnectionChangedHandler);
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

			_connectionDropdown = new IMGUIContainer(ConnectionDropdownDrawHandler)
			{
				tooltip = "Where the graph is read from. Same connection target as the Profiler: " +
					"the Editor shows snapshots recorded in Play Mode, a player shows snapshots received from the device.",
			};
			_connectionDropdown.style.alignSelf = Align.Center;
			toolbar.Add(_connectionDropdown);

			_editorControls = new VisualElement();
			_editorControls.style.flexDirection = FlexDirection.Row;
			_snapshotsMenu = new ToolbarMenu { text = DefaultSnapshotsMenuText };
			_editorControls.Add(_snapshotsMenu);
			_editorControls.Add(new ToolbarButton(OpenFileButtonClickHandler) { text = "Open File..." });
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
			toolbar.Add(_deviceControls);

			_exportMenu = new ToolbarMenu
			{
				text = "Export",
				tooltip = "Write the shown graph to a file. Graphs are never saved automatically.",
			};
			_exportMenu.menu.AppendAction("XML...", _ => ExportSnapshot(ExportFormat.Xml));
			_exportMenu.menu.AppendAction("HTML...", _ => ExportSnapshot(ExportFormat.Html));
			toolbar.Add(_exportMenu);

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

			return toolbar;
		}

		private void ApplySource()
		{
			bool isDevice = IsDeviceSource;
			_editorControls.style.display = isDevice ? DisplayStyle.None : DisplayStyle.Flex;
			_deviceControls.style.display = isDevice ? DisplayStyle.Flex : DisplayStyle.None;

			if (isDevice)
			{
				RefreshDeviceMenu();
				bool isSelectedVisible = _selectedDevice != null &&
					(!IsFilteringByTarget(InitializationGraphDeviceSource.Snapshots) || IsMatchingTarget(_selectedDevice));

				LoadDeviceSnapshot(isSelectedVisible ? _selectedDevice : GetLatestDeviceSnapshot());
				return;
			}

			RefreshSnapshotsMenu();
			IReadOnlyList<RecordedSnapshot> recordedSnapshots = InitializationGraphHistory.Snapshots;
			if ((_isShowingFile || recordedSnapshots.Count == 0) && File.Exists(_selectedPath))
			{
				LoadSnapshot(_selectedPath);
				return;
			}

			bool isSelectedRecordedVisible = IsRecorded(recordedSnapshots, _selectedRecorded);
			LoadRecordedSnapshot(isSelectedRecordedVisible ? _selectedRecorded : GetLatestRecordedSnapshot());
		}

		private DeviceSnapshot GetLatestDeviceSnapshot()
		{
			IReadOnlyList<DeviceSnapshot> snapshots = InitializationGraphDeviceSource.Snapshots;
			bool isFiltered = IsFilteringByTarget(snapshots);
			for (int i = 0; i < snapshots.Count; i++)
			{
				DeviceSnapshot received = snapshots[i];
				if (!isFiltered || IsMatchingTarget(received))
				{
					return received;
				}
			}

			return null;
		}

		private bool IsFilteringByTarget(IReadOnlyList<DeviceSnapshot> snapshots)
		{
			if (_isShowingAllDevices || _connectionState == null ||
				_connectionState.connectedToTarget != ConnectionTarget.Player)
			{
				return false;
			}

			for (int i = 0; i < snapshots.Count; i++)
			{
				if (IsMatchingTarget(snapshots[i]))
				{
					return true;
				}
			}

			return false;
		}

		private bool IsMatchingTarget(DeviceSnapshot received) =>
			string.Equals(received.DeviceName, _connectionState?.connectionName, StringComparison.Ordinal);

		private void RefreshSnapshotsMenu()
		{
			DropdownMenu menu = _snapshotsMenu.menu;
			menu.MenuItems().Clear();

			IReadOnlyList<RecordedSnapshot> snapshots = InitializationGraphHistory.Snapshots;
			if (snapshots.Count == 0)
			{
				menu.AppendAction("No recorded snapshots", _ => { }, DropdownMenuAction.Status.Disabled);
				return;
			}

			foreach (RecordedSnapshot recorded in snapshots)
			{
				menu.AppendAction(
					recorded.Label,
					_ => LoadRecordedSnapshot(recorded),
					_ => recorded == _selectedRecorded ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
			}

			menu.AppendSeparator();
			menu.AppendAction("Clear", _ => ClearRecordedSnapshots());
		}

		private void ClearRecordedSnapshots()
		{
			InitializationGraphHistory.Clear();
			_selectedRecorded = null;
			_selectedPath = null;
			_isShowingFile = false;
			ApplySource();
		}

		private void RefreshDeviceMenu()
		{
			DropdownMenu menu = _deviceMenu.menu;
			menu.MenuItems().Clear();

			IReadOnlyList<DeviceSnapshot> snapshots = InitializationGraphDeviceSource.Snapshots;
			if (snapshots.Count == 0)
			{
				menu.AppendAction("No received snapshots", _ => { }, DropdownMenuAction.Status.Disabled);
				AppendShowAllDevicesAction(menu);
				return;
			}

			bool isFiltered = IsFilteringByTarget(snapshots);
			foreach (DeviceSnapshot received in snapshots)
			{
				if (isFiltered && !IsMatchingTarget(received))
				{
					continue;
				}

				menu.AppendAction(
					received.Label,
					_ => LoadDeviceSnapshot(received),
					_ => received == _selectedDevice ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
			}

			AppendShowAllDevicesAction(menu);
		}

		private void AppendShowAllDevicesAction(DropdownMenu menu)
		{
			menu.AppendSeparator();
			menu.AppendAction(
				"Show All Devices",
				_ => ToggleShowAllDevices(),
				_ => _isShowingAllDevices ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
		}

		private void ToggleShowAllDevices()
		{
			_isShowingAllDevices = !_isShowingAllDevices;
			RefreshDeviceMenu();
		}

		private void LoadRecordedSnapshot(RecordedSnapshot recorded)
		{
			_selectedRecorded = recorded;
			_isShowingFile = false;
			if (recorded == null)
			{
				ShowEmpty("No snapshot. Enable Record and enter Play Mode.");
				return;
			}

			_snapshotsMenu.text = recorded.Label;
			ShowSnapshot(recorded.Snapshot);
		}

		private void LoadSnapshot(string path)
		{
			_selectedRecorded = null;
			_isShowingFile = true;
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
				ShowEmpty(GetNoDeviceSnapshotMessage());
				return;
			}

			_deviceMenu.text = received.Label;
			ShowSnapshot(received.Snapshot);
		}

		private void ShowSnapshot(InitializationGraphSnapshot snapshot)
		{
			_currentSnapshot = snapshot;
			_exportMenu.SetEnabled(true);

			InitializationGraphLayout layout = InitializationGraphLayout.Build(snapshot);
			_summaryLabel.text = $"{snapshot.Status} · {InitializationTimeFormat.Format(snapshot.TotalMilliseconds)} · " +
				$"{snapshot.Systems.Count} systems ({layout.CriticalCount} critical) · {layout.LevelsCount} levels";
			_graphView.Show(layout);
		}

		private void ShowEmpty(string message)
		{
			_currentSnapshot = null;
			_exportMenu.SetEnabled(false);
			_snapshotsMenu.text = DefaultSnapshotsMenuText;
			_deviceMenu.text = DefaultSnapshotsMenuText;
			_summaryLabel.text = message;
			_graphView.Show(InitializationGraphLayout.Empty);
		}

		private string GetNoDeviceSnapshotMessage()
		{
			if (_connectionState?.connectedToTarget == ConnectionTarget.Player)
			{
				return $"Connected to {_connectionState.connectionName}. No snapshot yet - press Request.";
			}

			return "No device selected. Pick one in the dropdown " +
				$"(players: {InitializationGraphDeviceSource.ConnectedPlayersCount.ToString(CultureInfo.InvariantCulture)}).";
		}

		private void ConnectionDropdownDrawHandler()
		{
			if (_connectionState == null)
			{
				return;
			}

			PlayerConnectionGUILayout.ConnectionTargetSelectionDropdown(_connectionState, EditorStyles.toolbarDropDown);
		}

		private void ConnectionChangedHandler(string playerName)
		{
			if (_graphView == null)
			{
				return;
			}

			if (IsDeviceSource)
			{
				InitializationGraphDeviceSource.RequestSnapshot();
			}

			ApplySource();
		}

		private void PlayersChangedHandler()
		{
			if (_graphView != null && IsDeviceSource)
			{
				RefreshDeviceMenu();
			}

			Repaint();
		}

		private void DisposeConnectionState()
		{
			_connectionState?.Dispose();
			_connectionState = null;
		}

		private void RecordedHandler(RecordedSnapshot recorded)
		{
			if (_graphView == null || IsDeviceSource)
			{
				_selectedRecorded = recorded;
				return;
			}

			RefreshSnapshotsMenu();
			LoadRecordedSnapshot(recorded);
		}

		private void DeviceSnapshotReceivedHandler(DeviceSnapshot received)
		{
			if (_graphView == null || !IsDeviceSource)
			{
				_selectedDevice = received;
				return;
			}

			RefreshDeviceMenu();
			if (IsFilteringByTarget(InitializationGraphDeviceSource.Snapshots) && !IsMatchingTarget(received))
			{
				return;
			}

			LoadDeviceSnapshot(received);
		}

		private void OpenFileButtonClickHandler()
		{
			string path = EditorUtility.OpenFilePanel("Open Initialization Graph", InitializationGraphStorage.DirectoryPath, "xml");
			if (!string.IsNullOrEmpty(path))
			{
				LoadSnapshot(path);
			}
		}

		private void RequestButtonClickHandler()
		{
			int playersCount = InitializationGraphDeviceSource.ConnectedPlayersCount;
			if (playersCount == 0)
			{
				_summaryLabel.text = "No connected players. Pick a device in the dropdown and run a development build.";
				return;
			}

			InitializationGraphDeviceSource.RequestSnapshot();
		}

		private void ExportSnapshot(ExportFormat format)
		{
			if (_currentSnapshot == null)
			{
				return;
			}

			bool isXml = format == ExportFormat.Xml;
			string directoryPath = InitializationGraphStorage.DirectoryPath;
			Directory.CreateDirectory(directoryPath);
			string fileName = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
			string path = EditorUtility.SaveFilePanel(
				"Save Initialization Graph",
				directoryPath,
				fileName,
				isXml ? "xml" : "html");
			if (string.IsNullOrEmpty(path))
			{
				return;
			}

			try
			{
				if (isXml)
				{
					InitializationGraphStorage.ExportXml(_currentSnapshot, path);
					_selectedPath = path;
					return;
				}

				InitializationGraphStorage.ExportHtml(_currentSnapshot, path);
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

		private void AllEdgesToggleChangedHandler(ChangeEvent<bool> changeEvent)
		{
			_isShowingAllEdges = changeEvent.newValue;
			_graphView.IsShowingAllEdges = changeEvent.newValue;
		}

		private enum ExportFormat
		{
			Xml = 0,
			Html = 1,
		}
	}
}