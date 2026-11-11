using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

#if BRX_APP
using AcDb = Teigha.DatabaseServices;
#endif

namespace ELPROCAD.CAD.Library.BlockDefinitions
{
	public sealed class BlockDefinitionDocument
	{
		public int SchemaVersion { get; set; }

		public List<BlockDefinition> Blocks { get; set; } = new();
	}
}
