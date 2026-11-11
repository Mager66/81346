using ELPROCAD.CAD.Library.Analysis.Sheets;
using ELPROCAD.CAD.Library.Analysis.Terminals;
using ELPROCAD.CAD.Library.BlockDefinitions;
using ELPROCAD.CAD.Library.CadEntity;

#if BRX_APP
using AcDb = Teigha.DatabaseServices;
#endif

namespace ELPROCAD.CAD.Library.Analysis.Blocks
{
	public sealed class BlockReader
	{
		private readonly BlockDefinitionLookup definitionLookup;
		private readonly TerminalExtractor terminalExtractor;

		// TODO: BET1 is the incorrect legacy attribute tag.
		// It is used temporarily for existing test drawings.
		// Replace it with DES1 when the symbol definitions are migrated.
		private const string LocationAttributeTag = "BET1";

		public BlockReader(BlockDefinitionLookup definitionLookup, TerminalExtractor terminalExtractor)
		{
			this.definitionLookup = definitionLookup
				?? throw new ArgumentNullException(nameof(definitionLookup));

			this.terminalExtractor = terminalExtractor
				?? throw new ArgumentNullException(nameof(terminalExtractor));
		}

		public CadBlock Read(AcDb.Transaction transaction,	AcDb.ObjectId blockId)
		{
			if (transaction.GetObject(blockId, AcDb.OpenMode.ForRead) is not AcDb.BlockReference block)
				throw new ArgumentException("Objektet måste vara en blockreferens.", nameof(blockId));			

			var definitionId = block.IsDynamicBlock
				? block.DynamicBlockTableRecord
				: block.BlockTableRecord;

			var blockRecord = (AcDb.BlockTableRecord)transaction.GetObject(definitionId, AcDb.OpenMode.ForRead);

			var name = blockRecord.Name;
			var definition = definitionLookup.Find(name);
			var position = block.Position;

			var attributes = new List<CadAttribute>();
			foreach(AcDb.ObjectId attId in block.AttributeCollection)
			{
				if (transaction.GetObject(attId, AcDb.OpenMode.ForRead) is not AcDb.AttributeReference att)
					continue;

				if(attId.IsErased || !attId.IsValid) 
					continue;

				attributes.Add(new CadAttribute(objectId: attId, ownerId: block.ObjectId, tag: att.Tag, value: att.TextString));
			}

			if (definition == null)
			{
				return new CadBlock(
					objectId: blockId,
					blockName: name,
					position: position,
					attributes: attributes,
					objectType: null,
					terminals: Array.Empty<BlockTerminal>());
			}

			var terminals = terminalExtractor.Extract(
				transaction,
				blockId,
				definition);

			return new CadBlock(
				objectId: blockId,
				blockName: name,
				position: position,
				attributes: attributes,
				objectType: definition.ObjectType,
				terminals: terminals);
		}

		public IReadOnlyList<CadBlock> Read(AcDb.Database database, SheetAnalysisData analysis)
		{
			if (database == null)
				throw new ArgumentNullException(nameof(database));

			if (analysis == null)
				throw new ArgumentNullException(nameof(analysis));

			var blocks = new List<CadBlock>();

			using var transaction = database.TransactionManager.StartOpenCloseTransaction();

			foreach (var item in analysis.Blocks)
			{
				var attributes = new List<CadAttribute>();

				if (transaction.GetObject(item.ObjectId, AcDb.OpenMode.ForRead) is not AcDb.BlockReference block)
					continue;

				foreach (AcDb.ObjectId attId in block.AttributeCollection)
				{
					if (transaction.GetObject(attId, AcDb.OpenMode.ForRead) is not AcDb.AttributeReference attribute)
						continue;

					attributes.Add(new CadAttribute(attribute.ObjectId, item.ObjectId, attribute.Tag, attribute.TextString)
					{
						Height = attribute.Height,
						IsInvisible = !attribute.Visible
					});
				}

				var name = block.Name;
				var definition = definitionLookup.Find(name);
				var position = block.Position;

				IReadOnlyList<BlockTerminal>? terminals = null;

				if (definition != null)
					terminals = terminalExtractor.Extract(
						transaction,
						block.ObjectId,
						definition);

				blocks.Add(new CadBlock(
					objectId: item.ObjectId,
					blockName: item.BlockName,
					position: block.Position,
					attributes: attributes,
					objectType: definition?.ObjectType,
					terminals: terminals));				
			}
			return blocks.AsReadOnly();
		}
	}
}
