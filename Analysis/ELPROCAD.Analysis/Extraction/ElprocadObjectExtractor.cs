namespace ELPROCAD.Analysis.Extraction
{
	/// <summary>ELPROCAD extraction uses symbol attributes only; no boundary context is consulted.</summary>
	internal sealed class ElprocadObjectExtractor : IElprocadObjectExtractor
	{
		private readonly string _placementTag;
		private readonly string _designationTag;
		private readonly string _separator;

		internal ElprocadObjectExtractor(string profileId, string placementTag, string designationTag, string separator)
		{
			ProfileId = profileId;
			_placementTag = placementTag;
			_designationTag = designationTag;
			_separator = separator;
		}

		public string ProfileId { get; }
		public string PlacementAttributeTag => _placementTag;
		public string DesignationAttributeTag => _designationTag;

		public IObjectExtractionResult Extract(IEnumerable<(string Tag, string? Value)> attributes)
		{
			if (attributes == null) throw new ArgumentNullException(nameof(attributes));
			var values = attributes.ToArray();
			var errors = new List<(string AttributeTag, string Message)>();
			var placement = ReadRequired(_placementTag);
			var designation = ReadRequired(_designationTag);
			if (errors.Count > 0) return new ExtractionResult(Array.Empty<IExtractedAspect>(), errors);

			return new ExtractionResult(new IExtractedAspect[]
			{
				new ExtractedAspect(placement + _separator + designation)
			}, errors);

			string ReadRequired(string tag)
			{
				var matches = values.Where(x => string.Equals(x.Tag, tag, StringComparison.OrdinalIgnoreCase)).ToArray();
				if (matches.Length > 1)
				{
					errors.Add((tag, $"Attribute '{tag}' occurs more than once on the symbol."));
					return string.Empty;
				}
				var value = matches.Length == 1 ? matches[0].Value?.Trim() : null;
				if (string.IsNullOrEmpty(value))
				{
					errors.Add((tag, $"Attribute '{tag}' requires a value."));
					return string.Empty;
				}
				// Values are preserved as flat text; punctuation never creates additional levels.
				return value!;
			}
		}
	}

	internal sealed class ExtractedAspect : IExtractedAspect
	{
		internal ExtractedAspect(string designation) => Levels = Array.AsReadOnly(new[] { designation });
		public AspectKind Kind => AspectKind.Location;
		public AspectStructure Structure => AspectStructure.Flat;
		public IReadOnlyList<string> Levels { get; }
	}

	internal sealed class ExtractionResult : IObjectExtractionResult
	{
		internal ExtractionResult(IEnumerable<IExtractedAspect> aspects, IEnumerable<(string AttributeTag, string Message)> errors)
		{
			Aspects = Array.AsReadOnly(aspects.ToArray());
			Errors = Array.AsReadOnly(errors.ToArray());
		}
		public bool IsValid => Errors.Count == 0;
		public IReadOnlyList<IExtractedAspect> Aspects { get; }
		public IReadOnlyList<(string AttributeTag, string Message)> Errors { get; }
	}
}
