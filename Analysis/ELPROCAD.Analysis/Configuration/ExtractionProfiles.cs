using System.Globalization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using ELPROCAD.Analysis.Extraction;

namespace ELPROCAD.Analysis.Configuration
{
    /// <summary>Loads an explicitly selected profile and constructs its extraction strategy.</summary>
    public static class ExtractionProfiles
    {
        public static IElprocadObjectExtractor LoadElprocadJson(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            if (json.Length > 65536) throw new FormatException("The extraction profile exceeds the supported size.");
            RequireSingleRoot(json);
            XElement root;
            try
            {
                // The framework JSON reader avoids introducing a CAD or third-party JSON dependency.
                using var reader = JsonReaderWriterFactory.CreateJsonReader(
                    Encoding.UTF8.GetBytes(json), XmlDictionaryReaderQuotas.Max);
                root = XElement.Load(reader);
            }
            catch (Exception ex) when (ex is XmlException || ex is InvalidOperationException)
            {
                throw new FormatException("The extraction profile is not valid JSON.", ex);
            }

            var settings = ReadObject(root, "schemaVersion", "profileId", "extractor", "boundaries", "activeAspects", "location");
            var version = settings["schemaVersion"];
            if ((string?)version.Attribute("type") != "number" ||
                !int.TryParse(version.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number != 1)
                throw new FormatException("Only extraction schema version 1 is supported.");
            var profileId = ReadString(settings["profileId"]);
            if (string.IsNullOrWhiteSpace(profileId)) throw new FormatException("A profile identifier is required.");
            if (ReadString(settings["extractor"]) != "ELPROCAD")
                throw new FormatException("Only the ELPROCAD extractor is implemented. IEC81346 context extraction is not available yet.");
            if (ReadString(settings["boundaries"]) != "Ignore")
                throw new FormatException("The ELPROCAD extractor requires boundaries = Ignore.");
            var aspects = settings["activeAspects"];
            if ((string?)aspects.Attribute("type") != "array" || aspects.Elements().Count() != 1 ||
                ReadString(aspects.Elements().Single()) != "Location")
                throw new FormatException("The ELPROCAD extractor supports Location only.");

            var location = ReadObject(settings["location"], "structure", "placementAttribute", "designationAttribute", "separator");
            if (ReadString(location["structure"]) != "Flat")
                throw new FormatException("The ELPROCAD extractor requires a flat Location structure.");
            var placementTag = ReadString(location["placementAttribute"]);
            var designationTag = ReadString(location["designationAttribute"]);
            if (!ValidTag(placementTag) || !ValidTag(designationTag))
                throw new FormatException("Placement and designation attribute tags must be non-empty and contain no whitespace.");
            if (string.Equals(placementTag, designationTag, StringComparison.OrdinalIgnoreCase))
                throw new FormatException("Placement and designation must use different attribute tags.");
            var separator = ReadString(location["separator"]);
            if (separator.Length != 1 || char.IsWhiteSpace(separator[0]) || char.IsControl(separator[0]))
                throw new FormatException("A single visible separator character is required.");

            return new ElprocadObjectExtractor(profileId.Trim(), placementTag, designationTag, separator);
        }

        private static Dictionary<string, XElement> ReadObject(XElement element, params string[] names)
        {
            if ((string?)element.Attribute("type") != "object" || element.Attributes().Any(x => x.Name != "type"))
                throw new FormatException("A configuration object is required without JSON type metadata.");
            var result = new Dictionary<string, XElement>(StringComparer.Ordinal);
            foreach (var child in element.Elements())
            {
                var name = child.Name.LocalName;
                if (!names.Contains(name) || result.ContainsKey(name))
                    throw new FormatException($"Unknown or duplicate configuration setting '{name}'.");
                result.Add(name, child);
            }
            if (names.Any(name => !result.ContainsKey(name)))
                throw new FormatException("The extraction profile is missing required settings.");
            return result;
        }

        private static string ReadString(XElement element)
        {
            if ((string?)element.Attribute("type") != "string" || element.HasElements)
                throw new FormatException($"Configuration setting '{element.Name.LocalName}' must be a string.");
            return element.Value;
        }

        private static bool ValidTag(string value) =>
            !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsWhiteSpace);

        private static void RequireSingleRoot(string json)
        {
            // The framework reader accepts trailing JSON; enforce one object before parsing its contents.
            var text = json.Trim();
            if (text.Length == 0 || text[0] != '{') throw new FormatException("A JSON profile object is required.");
            var depth = 0;
            var quoted = false;
            var escaped = false;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (quoted)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') quoted = false;
                    continue;
                }
                if (c == '"') quoted = true;
                else if (c == '{') depth++;
                else if (c == '}' && --depth == 0)
                {
                    if (i != text.Length - 1) throw new FormatException("Unexpected content after the extraction profile.");
                    return;
                }
            }
            throw new FormatException("The extraction profile is incomplete.");
        }
    }
}
