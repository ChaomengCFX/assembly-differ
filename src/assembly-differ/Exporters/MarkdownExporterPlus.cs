using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using JustAssembly.Core;

namespace Differ.Exporters {
	/// <summary>
	/// 带颜色和缩进的Markdown导出
	/// </summary>
	public class MarkdownExporterPlus : IAssemblyComparisonExporter {
		public string Format { get; } = "markdown+";
		private record Inherit(int Depth, string Pre);

		public void Export(AssemblyComparison assemblyComparison, OutputWriterFactory factory) {
			// IDiffItem implementations are internal so parse from XML for now
			var xml = assemblyComparison.Diff.ToXml();
			var doc = XDocument.Parse(xml);
			var name = assemblyComparison.First.Name;
			using var writer = factory.Create(Path.ChangeExtension(name, "md"));

			writer.WriteLine($"## API Changes: `{Path.GetFileNameWithoutExtension(name)}`");
			writer.WriteLine();

			foreach (var typeElement in doc.Element("Assembly").Element("Module").Elements("Type"))
				WriteTypeElement(writer, typeElement, new Inherit(0, ""));
		}

		private void WriteTypeElement(OutputWriter writer, XElement typeElement, Inherit inherit) {
			var typeName = typeElement.Attribute("Name")?.Value;

			if (typeName == "XLua.DelegateBridge") return;

			if (typeName.Contains('.') && inherit.Depth == 0)
				typeName = Regex.Replace(typeName, @"(^\S+)\.([^\.]+$)", "$1.**$2**");
			else
				typeName = $"**{typeName}**";

			var diffType = (DiffType)Enum.Parse(typeof(DiffType), typeElement.Attribute("DiffType").Value);

			switch (diffType) {
				case DiffType.Deleted:
					writer.WriteLine($"{inherit.Pre}{new string('#', inherit.Depth + 2)} *<font color=#ff9900>[D]</font>* <font color=#ff3300>{typeName}</font> is deleted");
					break;
				case DiffType.Modified:
					WriteMemberElements(writer, typeName, typeElement, inherit);
					break;
				case DiffType.New:
					writer.WriteLine($"{inherit.Pre}{new string('#', inherit.Depth + 2)} *<font color=#ff9900>[A]</font>* <font color=#66ccff>{typeName}</font> is new");
					break;
				default:
					throw new ArgumentOutOfRangeException(diffType.ToString());
			}

			if (inherit.Depth == 0)
				writer.WriteLine("---");
		}

		private void WriteMemberElements(OutputWriter writer, string typeName, XElement typeElement, Inherit inherit) {

			var memberElements = typeElement.Elements("Method").Concat(typeElement.Elements("Property")).Concat(typeElement.Elements("Field"));
			var typeElements = typeElement.Elements("Type");

			writer.WriteLine($"{inherit.Pre}{new string('#', inherit.Depth + 2)} *<font color=#ff9900>[M]</font>* <font color=#ff6600>{typeName}</font>");

			var pre = inherit.Pre + "- ";

			foreach (var memberElement in memberElements) {
				var memberType = memberElement.Name;
				var memberName = memberElement.Attribute("Name")?.Value;
				if (!string.IsNullOrEmpty(memberName) && Enum.TryParse(memberElement.Attribute("DiffType")?.Value, out DiffType diffType)) {
					switch (diffType) {
						case DiffType.Deleted:
							writer.WriteLine($"{pre}[{memberType}] `{memberName}` is deleted");
							break;
						case DiffType.Modified:
							var diffItem = memberElement.Descendants("DiffItem").FirstOrDefault();
							if (diffItem != null) {
								writer.WriteLine($"{pre}[{memberType}] `{memberName}`");
								writer.WriteLine(
									Regex.Replace(diffItem.Value, "changed from (.*?) to (.*).", "changed from `$1` to `$2`."));
							}
							else
								writer.WriteLine($"{pre}[{memberType}] `{memberName}` is added");
							break;
						case DiffType.New:
							writer.WriteLine($"{pre}[{memberType}] `{memberName}` is added");
							break;
						default:
							throw new ArgumentOutOfRangeException();
					}
				}
			}

			foreach (var nestedtypeElement in typeElements) {
				WriteTypeElement(writer, nestedtypeElement, new Inherit(inherit.Depth + 1, pre));
			}
		}
	}
}
