using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DTech.Pulse.Editor
{
	internal static class InitializationGraphHtmlReport
	{
		private const string TemplateName = "InitializationGraphReport";
		private const string TemplatePathSuffix = "/Editor/Html/InitializationGraphReport.html";
		private const string DataPlaceholder = "{{GRAPH_DATA}}";
		private const string HeatFormat = "0.###";
		private const char LineSeparator = (char)0x2028;
		private const char ParagraphSeparator = (char)0x2029;
		private const int NotStartedOrder = -1;

		public static string Build(InitializationGraphSnapshot snapshot)
		{
			if (snapshot == null)
			{
				throw new ArgumentNullException(nameof(snapshot));
			}

			return LoadTemplate().Replace(DataPlaceholder, BuildData(snapshot));
		}

		private static string LoadTemplate()
		{
			string[] guids = AssetDatabase.FindAssets($"{TemplateName} t:TextAsset");
			for (int i = 0; i < guids.Length; i++)
			{
				string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
				if (!assetPath.EndsWith(TemplatePathSuffix, StringComparison.Ordinal))
				{
					continue;
				}

				var template = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
				if (template != null)
				{
					return template.text;
				}
			}

			throw new InvalidOperationException(
				$"HTML report template not found. Expected a text asset at '*{TemplatePathSuffix}'.");
		}

		private static string BuildData(InitializationGraphSnapshot snapshot)
		{
			InitializationGraphLayout layout = InitializationGraphLayout.Build(snapshot);
			IReadOnlyList<InitializationSystemRecord> systems = snapshot.Systems;

			var builder = new StringBuilder(1024);
			builder.Append("{\"status\":");
			AppendString(builder, snapshot.Status.ToString());
			builder.Append(",\"recordedAtUtc\":");
			AppendString(builder, snapshot.RecordedAtUtc);
			builder.Append(",\"totalTime\":");
			AppendString(builder, InitializationTimeFormat.Format(snapshot.TotalMilliseconds));
			builder.Append(",\"systemsCount\":").Append(Format(systems.Count));
			builder.Append(",\"criticalCount\":").Append(Format(layout.CriticalCount));
			builder.Append(",\"levelsCount\":").Append(Format(layout.LevelsCount));
			AppendLevels(builder, layout);
			AppendNodes(builder, layout);
			AppendEdges(builder, layout);
			builder.Append('}');
			return builder.ToString();
		}

		private static void AppendLevels(StringBuilder builder, InitializationGraphLayout layout)
		{
			builder.Append(",\"levels\":[");
			for (int i = 0; i < layout.LevelEntries.Count; i++)
			{
				InitializationGraphLayout.LevelEntry level = layout.LevelEntries[i];
				if (i > 0)
				{
					builder.Append(',');
				}

				builder.Append("{\"level\":").Append(Format(level.Level));
				builder.Append(",\"title\":");
				AppendString(builder, level.Title);
				builder.Append(",\"isCritical\":").Append(level.IsCritical ? "true" : "false");
				builder.Append(",\"count\":").Append(Format(level.Indices.Length));
				builder.Append('}');
			}

			builder.Append(']');
		}

		private static void AppendNodes(StringBuilder builder, InitializationGraphLayout layout)
		{
			IReadOnlyList<InitializationSystemRecord> systems = layout.Systems;
			builder.Append(",\"nodes\":[");
			for (int i = 0; i < systems.Count; i++)
			{
				InitializationSystemRecord system = systems[i];
				if (i > 0)
				{
					builder.Append(',');
				}

				builder.Append("{\"id\":").Append(Format(i));
				builder.Append(",\"name\":");
				AppendString(builder, system.TypeName);
				builder.Append(",\"fullName\":");
				AppendString(builder, system.FullTypeName);
				builder.Append(",\"level\":").Append(Format(layout.Levels[i]));
				builder.Append(",\"row\":").Append(Format(layout.Rows[i]));
				builder.Append(",\"order\":")
					.Append(Format(system.StartOrder >= 0 ? system.StartOrder + 1 : NotStartedOrder));
				builder.Append(",\"critical\":").Append(layout.CriticalSystems[i] ? "true" : "false");
				builder.Append(",\"autoCritical\":").Append(layout.IsAutoCritical(i) ? "true" : "false");
				builder.Append(",\"status\":");
				AppendString(builder, system.Status.ToString());
				builder.Append(",\"start\":");
				AppendString(builder, $"+{InitializationTimeFormat.Format(system.StartMilliseconds)}");
				builder.Append(",\"duration\":");
				AppendString(builder, InitializationTimeFormat.Format(system.DurationMilliseconds));
				builder.Append(",\"heat\":").Append(Format(GetHeat(system, layout.MaxDurationMilliseconds)));
				builder.Append(",\"error\":");
				AppendString(builder, system.Error);
				builder.Append('}');
			}

			builder.Append(']');
		}

		private static void AppendEdges(StringBuilder builder, InitializationGraphLayout layout)
		{
			IReadOnlyList<InitializationSystemRecord> systems = layout.Systems;
			builder.Append(",\"edges\":[");
			bool isFirst = true;
			for (int i = 0; i < systems.Count; i++)
			{
				IReadOnlyList<int> dependencyIndices = systems[i].DependencyIndices;
				for (int j = 0; j < dependencyIndices.Count; j++)
				{
					int dependencyIndex = dependencyIndices[j];
					if (dependencyIndex < 0 || dependencyIndex >= systems.Count)
					{
						continue;
					}

					if (!isFirst)
					{
						builder.Append(',');
					}

					isFirst = false;
					builder.Append("{\"from\":").Append(Format(dependencyIndex));
					builder.Append(",\"to\":").Append(Format(i));
					builder.Append(",\"redundant\":")
						.Append(layout.IsRedundantDependency(i, j) ? "true" : "false");
					builder.Append('}');
				}
			}

			builder.Append(']');
		}

		private static double GetHeat(InitializationSystemRecord system, double maxDurationMilliseconds)
		{
			if (maxDurationMilliseconds <= 0d)
			{
				return 0d;
			}

			double heat = system.DurationMilliseconds / maxDurationMilliseconds;
			return heat < 0d ? 0d : Math.Min(heat, 1d);
		}

		private static string Format(int value)
		{
			return value.ToString(CultureInfo.InvariantCulture);
		}

		private static string Format(double value)
		{
			return value.ToString(HeatFormat, CultureInfo.InvariantCulture);
		}

		private static void AppendString(StringBuilder builder, string value)
		{
			builder.Append('"');
			if (string.IsNullOrEmpty(value))
			{
				builder.Append('"');
				return;
			}

			for (int i = 0; i < value.Length; i++)
			{
				char character = value[i];
				switch (character)
				{
					case '"':
					{
						builder.Append("\\\"");
					} break;

					case '\\':
					{
						builder.Append("\\\\");
					} break;

					case '\b':
					{
						builder.Append("\\b");
					} break;

					case '\f':
					{
						builder.Append("\\f");
					} break;

					case '\n':
					{
						builder.Append("\\n");
					} break;

					case '\r':
					{
						builder.Append("\\r");
					} break;

					case '\t':
					{
						builder.Append("\\t");
					} break;
					
					case '<':
					case '>':
					case '&':
					case LineSeparator:
					case ParagraphSeparator:
					{
						AppendUnicode(builder, character);
					} break;

					default:
					{
						if (character < ' ')
						{
							AppendUnicode(builder, character);
							break;
						}

						builder.Append(character);
					} break;
				}
			}

			builder.Append('"');
		}

		private static void AppendUnicode(StringBuilder builder, char character)
		{
			builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
		}
	}
}