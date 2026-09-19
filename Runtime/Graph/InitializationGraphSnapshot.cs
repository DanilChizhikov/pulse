using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using UnityEngine.Scripting;

namespace DTech.Pulse
{
	/// <summary>
	/// Result of a single initialization run: its systems, their order and timings.
	/// </summary>
	[Preserve]
	public sealed class InitializationGraphSnapshot
	{
		private const string RootElementName = "InitializationGraph";
		private const string SystemsElementName = "Systems";
		private const string SystemElementName = "System";
		private const string ErrorElementName = "Error";
		private const char DependencySeparator = ' ';

		/// <summary>
		/// UTC time the snapshot was recorded at, in round-trip ("O") format.
		/// </summary>
		public string RecordedAtUtc { get; }

		/// <summary>
		/// Final status of the recorded initialization run.
		/// </summary>
		public InitializationGraphStatus Status { get; }

		/// <summary>
		/// Total duration of the initialization run, in milliseconds.
		/// </summary>
		public double TotalMilliseconds { get; }

		/// <summary>
		/// Recorded systems, ordered the same way they were registered in the builder.
		/// </summary>
		public IReadOnlyList<InitializationSystemRecord> Systems { get; }

		internal InitializationGraphSnapshot(
			string recordedAtUtc,
			InitializationGraphStatus status,
			double totalMilliseconds,
			List<InitializationSystemRecord> systems)
		{
			RecordedAtUtc = recordedAtUtc;
			Status = status;
			TotalMilliseconds = totalMilliseconds;
			Systems = systems ?? throw new ArgumentNullException(nameof(systems));
		}

		/// <summary>
		/// Serializes the snapshot to XML.
		/// </summary>
		/// <param name="prettyPrint">Whether the XML should be formatted for reading.</param>
		/// <returns>The XML representation of the snapshot.</returns>
		public string ToXml(bool prettyPrint = false)
		{
			var systemsElement = new XElement(SystemsElementName);
			foreach (InitializationSystemRecord system in Systems)
			{
				systemsElement.Add(ToElement(system));
			}

			var root = new XElement(RootElementName,
				new XAttribute(XmlPropertyName.RecordedAtUtc, RecordedAtUtc ?? string.Empty),
				new XAttribute(XmlPropertyName.Status, Status.ToString()),
				new XAttribute(XmlPropertyName.TotalMilliseconds, XmlConvert.ToString(TotalMilliseconds)),
				systemsElement);

			return new XDocument(root)
				.ToString(prettyPrint ? SaveOptions.None : SaveOptions.DisableFormatting);
		}

		/// <summary>
		/// Restores a snapshot from its XML representation.
		/// </summary>
		/// <param name="xml">XML produced by <see cref="ToXml"/>.</param>
		/// <returns>The deserialized snapshot.</returns>
		/// <exception cref="ArgumentException">
		/// Thrown when <paramref name="xml"/> is null, empty or does not contain a snapshot.
		/// </exception>
		public static InitializationGraphSnapshot FromXml(string xml)
		{
			if (string.IsNullOrEmpty(xml))
			{
				throw new ArgumentException("Xml cannot be null or empty.", nameof(xml));
			}

			XElement root;
			try
			{
				root = XDocument.Parse(xml).Root;
			}
			catch (XmlException exception)
			{
				throw new ArgumentException("Xml does not contain an initialization graph snapshot.", nameof(xml), exception);
			}

			if (root == null || root.Name != RootElementName)
			{
				throw new ArgumentException("Xml does not contain an initialization graph snapshot.", nameof(xml));
			}

			try
			{
				var systems = new List<InitializationSystemRecord>();
				XElement systemsElement = root.Element(SystemsElementName);
				if (systemsElement != null)
				{
					foreach (XElement systemElement in systemsElement.Elements(SystemElementName))
					{
						systems.Add(ToRecord(systemElement));
					}
				}

				return new InitializationGraphSnapshot(
					ReadString(root, XmlPropertyName.RecordedAtUtc),
					ReadEnum<InitializationGraphStatus>(root, XmlPropertyName.Status),
					ReadDouble(root, XmlPropertyName.TotalMilliseconds),
					systems);
			}
			catch (Exception exception) when (exception is FormatException || exception is OverflowException)
			{
				throw new ArgumentException("Xml does not contain an initialization graph snapshot.", nameof(xml), exception);
			}
		}

		private static XElement ToElement(InitializationSystemRecord record)
		{
			var element = new XElement(SystemElementName,
				new XAttribute(XmlPropertyName.TypeName, record.TypeName ?? string.Empty),
				new XAttribute(XmlPropertyName.FullTypeName, record.FullTypeName ?? string.Empty),
				new XAttribute(XmlPropertyName.StartOrder, XmlConvert.ToString(record.StartOrder)),
				new XAttribute(XmlPropertyName.IsCritical, XmlConvert.ToString(record.IsCritical)),
				new XAttribute(XmlPropertyName.Status, record.Status.ToString()),
				new XAttribute(XmlPropertyName.StartMilliseconds, XmlConvert.ToString(record.StartMilliseconds)),
				new XAttribute(XmlPropertyName.DurationMilliseconds, XmlConvert.ToString(record.DurationMilliseconds)));

			if (record.IsAutoCritical)
			{
				element.Add(new XAttribute(XmlPropertyName.IsAutoCritical, XmlConvert.ToString(true)));
			}

			if (record.DependencyIndices.Count > 0)
			{
				element.Add(new XAttribute(XmlPropertyName.Dependencies, ToDependencies(record.DependencyIndices)));
			}

			if (!string.IsNullOrEmpty(record.Error))
			{
				element.Add(new XElement(ErrorElementName, record.Error));
			}

			return element;
		}

		private static InitializationSystemRecord ToRecord(XElement element)
		{
			return new InitializationSystemRecord(
				ReadString(element, XmlPropertyName.TypeName),
				ReadString(element, XmlPropertyName.FullTypeName),
				ReadInt(element, XmlPropertyName.StartOrder),
				ReadBool(element, XmlPropertyName.IsCritical),
				ReadBool(element, XmlPropertyName.IsAutoCritical),
				ReadEnum<InitializationSystemStatus>(element, XmlPropertyName.Status),
				ReadDouble(element, XmlPropertyName.StartMilliseconds),
				ReadDouble(element, XmlPropertyName.DurationMilliseconds),
				ReadDependencies(element, XmlPropertyName.Dependencies),
				element.Element(ErrorElementName)?.Value);
		}

		private static string ToDependencies(IReadOnlyList<int> indices)
		{
			var builder = new StringBuilder();
			for (int i = 0; i < indices.Count; i++)
			{
				if (i > 0)
				{
					builder.Append(DependencySeparator);
				}

				builder.Append(XmlConvert.ToString(indices[i]));
			}

			return builder.ToString();
		}

		private static string ReadString(XElement element, string attributeName)
		{
			return element.Attribute(attributeName)?.Value ?? string.Empty;
		}

		private static int ReadInt(XElement element, string attributeName)
		{
			string value = element.Attribute(attributeName)?.Value;
			return string.IsNullOrEmpty(value) ? 0 : XmlConvert.ToInt32(value);
		}

		private static double ReadDouble(XElement element, string attributeName)
		{
			string value = element.Attribute(attributeName)?.Value;
			return string.IsNullOrEmpty(value) ? 0d : XmlConvert.ToDouble(value);
		}

		private static bool ReadBool(XElement element, string attributeName)
		{
			string value = element.Attribute(attributeName)?.Value;
			return !string.IsNullOrEmpty(value) && XmlConvert.ToBoolean(value);
		}

		private static TEnum ReadEnum<TEnum>(XElement element, string attributeName)
			where TEnum : struct, Enum
		{
			string value = element.Attribute(attributeName)?.Value;
			return Enum.TryParse(value, out TEnum result) ? result : default;
		}

		private static int[] ReadDependencies(XElement element, string attributeName)
		{
			string value = element.Attribute(attributeName)?.Value;
			if (string.IsNullOrEmpty(value))
			{
				return Array.Empty<int>();
			}

			string[] parts = value.Split(new[] { DependencySeparator }, StringSplitOptions.RemoveEmptyEntries);
			var indices = new int[parts.Length];
			for (int i = 0; i < parts.Length; i++)
			{
				indices[i] = XmlConvert.ToInt32(parts[i]);
			}

			return indices;
		}
	}
}