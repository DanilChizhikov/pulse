using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace DTech.Pulse
{
	internal static class InitializationGraphXmlReport
	{
		public static string Build(InitializationGraphSnapshot snapshot, bool prettyPrint = false)
		{
			var systemsElement = new XElement(InitializationGraphSnapshot.SystemsElementName);
			foreach (InitializationSystemRecord system in snapshot.Systems)
			{
				systemsElement.Add(ToElement(system));
			}

			var root = new XElement(InitializationGraphSnapshot.RootElementName,
				new XAttribute(XmlPropertyName.RecordedAtUtc, snapshot.RecordedAtUtc ?? string.Empty),
				new XAttribute(XmlPropertyName.Status, snapshot.Status.ToString()),
				new XAttribute(XmlPropertyName.TotalMilliseconds, XmlConvert.ToString(snapshot.TotalMilliseconds)),
				systemsElement);

			return new XDocument(root)
				.ToString(prettyPrint ? SaveOptions.None : SaveOptions.DisableFormatting);
		}
		
		private static XElement ToElement(InitializationSystemRecord record)
		{
			var element = new XElement(InitializationGraphSnapshot.SystemElementName,
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
				element.Add(new XElement(InitializationGraphSnapshot.ErrorElementName, record.Error));
			}

			return element;
		}
		
		private static string ToDependencies(IReadOnlyList<int> indices)
		{
			var builder = new StringBuilder();
			for (int i = 0; i < indices.Count; i++)
			{
				if (i > 0)
				{
					builder.Append(InitializationGraphSnapshot.DependencySeparator);
				}

				builder.Append(XmlConvert.ToString(indices[i]));
			}

			return builder.ToString();
		}
	}
}