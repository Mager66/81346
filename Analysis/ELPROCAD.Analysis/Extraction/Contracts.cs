namespace ELPROCAD.Analysis.Extraction
{
    public enum AspectKind
    {
        PowerFunction, ControlFunction, TopographicStructure, Location, Function, Product
    }

    public enum AspectStructure { Flat, Hierarchical }

    public interface IExtractedAspect
    {
        AspectKind Kind { get; }
        AspectStructure Structure { get; }
        /// <summary>Flat results contain exactly one level; consumers must not split it.</summary>
        IReadOnlyList<string> Levels { get; }
    }

    public interface IObjectExtractionResult
    {
        bool IsValid { get; }
        /// <summary>No aspects are returned when any extraction error exists.</summary>
        IReadOnlyList<IExtractedAspect> Aspects { get; }
        IReadOnlyList<(string AttributeTag, string Message)> Errors { get; }
    }

    public interface IElprocadObjectExtractor
    {
        string ProfileId { get; }

		string PlacementAttributeTag { get; }
		
        string DesignationAttributeTag { get; }
		
        /// <summary>
		/// Extracts one object's aspects from captured attributes without accessing CAD or a database.
		/// The ELPROCAD strategy ignores boundary boxes as designation sources.
		/// This attribute-only entry point does not implement IEC 81346 context extraction.
		/// </summary>
		IObjectExtractionResult Extract(IEnumerable<(string Tag, string? Value)> attributes);
    }
}
