#if BRX_APP
using AcDb = Teigha.DatabaseServices;
#endif

namespace ELPROCAD.CAD.Commands.Analysis
{
	internal sealed class AnalyzedBlock
	{
		public AcDb.ObjectId BlockId { get; }

		public IReadOnlyDictionary<string, string> Attributes { get; }

		public AnalyzedBlock(AcDb.ObjectId blockId,	IDictionary<string, string> attributes)
		{
			if (attributes == null)
				throw new ArgumentNullException(nameof(attributes));

			BlockId = blockId;

			Attributes = new System.Collections.ObjectModel
				.ReadOnlyDictionary<string, string>(
					new Dictionary<string, string>(
						attributes,
						StringComparer.OrdinalIgnoreCase));
		}
	}
}
