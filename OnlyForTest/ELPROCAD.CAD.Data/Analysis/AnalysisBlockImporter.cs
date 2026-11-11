using DevExpress.Xpo;
using ELPROCAD.CAD.Data.CRUD;
using ELPROCAD.CAD.Data.CRUD.Analisys;
using ELPROCAD.CAD.Data.Model;
using ELPROCAD.CAD.Library.Analysis.Sheets;
using ELPROCAD.CAD.Library.CadEntity;
using ELPROCAD.DataAccess.Contracts.MDBX.Aspects;
using ELPROCAD.DataAccess.Contracts.MDBX.Documents;
using ELPROCAD.DataAccess.Contracts.MDBX.Objects;
using ELPROCAD.DataAccess.Repositories.MDBX;
using ELPROCAD.DataAccess.Repositories.PDSX;
using FluentValidation.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#if ARX_APP
using AcAp = Autodesk.AutoCAD.ApplicationServices;
using AcDb = Autodesk.AutoCAD.DatabaseServices;
#elif BRX_APP
using AcAp = Bricscad.ApplicationServices;
using AcDb = Teigha.DatabaseServices;
#endif

namespace ELPROCAD.CAD.Data.Analysis
{
	public sealed class AnalysisBlockImporter
	{
		//private readonly UnitOfWork _unitOfWork;
		//private readonly QueryEngineeringObject _objects;
		private readonly IPdsxRepository _pdsx;
		private readonly IMdbxRepository _mdbx;

		// TODO: BET1 is the incorrect legacy attribute tag.
		// Replace it with DES1 when the symbols are migrated.
		private const string LocationAttributeTag = "BET1";

		// TODO: Load designation syntax from project configuration.
		private const string LocationPrefix = "+";
		private const char LevelSeparator = '.';

		/// <summary>
		/// Identifies the object link inherited by an unregistered copied main symbol.
		/// </summary>
		internal Guid? CopiedObjectId { get; set; }

		public AnalysisBlockImporter(IPdsxRepository pdsx, IMdbxRepository mdbx)
		{
			_pdsx = pdsx ?? throw new ArgumentNullException(nameof(pdsx));
			_mdbx = mdbx ?? throw new ArgumentNullException(nameof(mdbx));
		}

		/// <summary>
		/// Imports location paths from an analyzed sheet.
		/// Returns the number of newly created location nodes.
		/// </summary>
		public (int CreatedLocations, int ProcessedBlocks, IReadOnlyList<(AcDb.ObjectId BlockId, string Message)> Errors) Import(AcAp.Document doc, SheetAnalysisData result)
		{
			if (doc == null)
				throw new ArgumentNullException(nameof(doc));

			if (result == null)
				throw new ArgumentNullException(nameof(result));

			if (!Path.IsPathRooted(doc.Name))
				throw new InvalidOperationException("Save the drawing before importing.");

			if (string.IsNullOrWhiteSpace(result.TitleBlock.SheetLayer))
				throw new InvalidOperationException("The title block does not contain a sheet number.");

			var projects = _pdsx.Project.GetAll();

			if (projects.Count != 1)
				throw new InvalidOperationException("The analysis database must contain exactly one project.");

			if (!Path.IsPathRooted(doc.Name))
				throw new InvalidOperationException("Save the drawing before importing.");

			if (string.IsNullOrWhiteSpace(result.TitleBlock.SheetLayer))
				throw new InvalidOperationException("The title block does not contain a sheet number.");

			var project = projects[0];

			var document = GetOrCreateDocument(project.Id, doc.Name);
			var sheet = GetOrCreateSheet(document.Id, result.TitleBlock.SheetLayer);
			var originalLocations = _mdbx.Location.GetByProjectId(project.Id).ToDictionary(location => location.Id);
			var originalPaths = originalLocations.Values.ToDictionary(location => location.Id, location => ReadPath(location.Id, originalLocations));
			var items = new List<ImportBlock>();
			var errors = new List<(AcDb.ObjectId BlockId, string Message)>();
			var failedBlocks = new HashSet<AcDb.ObjectId>();
			var resolvedObjects = new Dictionary<AcDb.ObjectId, Guid>();

			void Fail(AcDb.ObjectId blockId, string message)
			{
				if (failedBlocks.Add(blockId))
					errors.Add((blockId, message));
			}

			// Pass 1: resolve registered symbol identities before interpreting XData.
			foreach (var block in result.Blocks)
			{
				try
				{
					var xdataId = EngineeringObjectLink.Read(
						doc.Database,
						block.ObjectId);

					var registered = _mdbx.CadSymbolLink.GetByBlock(
						document.Id,
						block.ObjectId.Handle.Value);

					if (registered != null && registered.SheetId != sheet.Id)
					{
						throw new InvalidOperationException(
							"The CAD symbol is registered on another sheet.");
					}

					if (registered != null &&
						xdataId.HasValue &&
						registered.EngineeringObjectId != xdataId.Value)
					{
						throw new InvalidOperationException(
							"The registered symbol link and XData reference different objects.");
					}

					var linkedId = registered?.EngineeringObjectId ?? xdataId;

					var attribute = block.GetAttribute(LocationAttributeTag);

					if (attribute == null ||
						string.IsNullOrWhiteSpace(attribute.Value))
					{
						if (linkedId.HasValue)
						{
							Fail(
								block.ObjectId,
								"The linked block has no location designation.");
						}

						continue;
					}

					var item = new ImportBlock(
						block,
						attribute,
						ParseLocation(attribute.Value),
						attribute.Value.Trim().EndsWith(
							LevelSeparator.ToString(),
							StringComparison.Ordinal));

					if (registered == null &&
						xdataId.HasValue &&
						!item.IsMember)
					{
						var originalObject = _mdbx.EngineeringObject
							.GetById(xdataId.Value)
							?? throw new InvalidOperationException(
								"The object referenced by the unregistered symbol does not exist.");

						EnsureObjectIsActive(originalObject);

						if (originalObject.ProjectId != project.Id)
						{
							throw new InvalidOperationException(
								"The unregistered symbol references an object in another project.");
						}

						var registeredSymbols = _mdbx.CadSymbolLink
							.GetByObjectId(xdataId.Value);

						if (registeredSymbols.Count > 0)
						{
							// This block is not registered, but its inherited object ID is.
							// Do not let it rename the original engineering object.
							item.CopiedObjectId = xdataId.Value;
							linkedId = null;
						}
					}

					item.LinkedObjectId = linkedId;

					if (linkedId.HasValue)
					{
						var entity = _mdbx.EngineeringObject.GetById(linkedId.Value)
							?? throw new InvalidOperationException(
								"The linked engineering object no longer exists.");


						var location = GetRepresentedLocation(entity, project.Id);

						if (!originalPaths.TryGetValue(
							location.Id,
							out var originalPath))
						{
							throw new InvalidOperationException(
								"The object's location was not found in the project snapshot.");
						}

						item.SourceLocationId = location.Id;
						item.OriginalLevels = originalPath;
					}

					items.Add(item);
				}
				catch (Exception exception) when (
					exception is ArgumentException ||
					exception is InvalidOperationException)
				{
					Fail(block.ObjectId, exception.Message);
				}
			}

			// Group main symbols by their persistent location identity.
			var groups = items
				.Where(item => !item.IsMember && item.LinkedObjectId.HasValue)
				.GroupBy(item => item.SourceLocationId!.Value)
				.OrderBy(group => group.First().OriginalLevels.Length)
				.ThenBy(group => group.Key)
				.ToList();

			// Multiple symbols for one object may contain unchanged text.
			// Only explicit changes contribute a requested new path.
			foreach (var group in groups)
			{
				var first = group.First();

				var hasDifferentDesignations = group.Any(item =>
						!SamePath(
							item.RequestedLevels,
							first.RequestedLevels));

				if (!hasDifferentDesignations)
					continue;							
				
				foreach (var item in group)
				{
					Fail(
						item.Block.ObjectId,
							"Main symbols share the same engineering object identifier " +
							"but have different designations. A copied symbol may require " +
							"a new engineering object. No change was applied to this group.");
				}
				
			}

			// Reject competing explicit requests at the same depth.
			var requests = groups
				.Where(group => !group.Any(item =>
					failedBlocks.Contains(item.Block.ObjectId)))
				.Select(group => new
				{
					Group = group,
					Request = group.FirstOrDefault(item =>
						!SamePath(item.RequestedLevels, item.OriginalLevels))
				})
				.Where(item => item.Request != null)
				.ToList();

			foreach (var depthGroup in requests.GroupBy(item => item.Request!.RequestedLevels.Length))
			{
				foreach (var collision in depthGroup.GroupBy(
					item => item.Request!.RequestedLevels.Last(),
					StringComparer.OrdinalIgnoreCase)
					.Where(group => group.Count() > 1))
				{
					foreach (var request in collision)
					{
						foreach (var item in request.Group)
						{
							Fail(
								item.Block.ObjectId,
								"Different objects request the same designation at the same depth.");
						}
					}
				}
			}

			// Pass 2: apply explicit changes, processing ancestors first.
			foreach (var group in groups)
			{
				if (group.Any(item => failedBlocks.Contains(item.Block.ObjectId)))
					continue;

				try
				{
					var changed = group.FirstOrDefault(item => !SamePath(item.RequestedLevels, item.OriginalLevels));

					if (changed != null)
					{
						var targetLevels = RebaseParentPath(
							changed.RequestedLevels,
							originalPaths);

						var update = UpdateLinkedLocation(
							changed.LinkedObjectId!.Value,
							project.Id,
							targetLevels);

						if (!update.Validation.IsValid)
						{
							var message = string.Join(
								Environment.NewLine,
								update.Validation.Errors
									.Select(error => error.ErrorMessage)
									.Distinct());

							foreach (var item in group)
								Fail(item.Block.ObjectId, message);

							continue;
						}
					}

					foreach (var item in group)
					{
						resolvedObjects[item.Block.ObjectId] =
							item.LinkedObjectId!.Value;
					}
				}
				catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
				{
					foreach (var item in group)
						Fail(item.Block.ObjectId, exception.Message);
				}
			}

			// Import previously unlinked main symbols after existing identities.
			foreach (var item in items
				.Where(item => !item.IsMember && !item.LinkedObjectId.HasValue)
				.OrderBy(item => item.RequestedLevels.Length)
				.ThenBy(item => item.Block.ObjectId.Handle.Value))
			{
				try
				{
					var levels = RebaseParentPath(
						item.RequestedLevels,
						originalPaths);

					var location = ResolveLocationPath(
						project.Id,
						levels,
						createMissing: true);

					if (item.CopiedObjectId.HasValue &&	location.RepresentedByObjectId.HasValue)
					{
						throw new InvalidOperationException(
							"The copied main symbol requires an unused designation. " +
							"The requested location already represents an engineering object.");
					}

					var entity = GetOrCreateObject(
						project.Id,
						location.Id);

					item.SourceLocationId = location.Id;
					resolvedObjects[item.Block.ObjectId] = entity.Id;
				}
				catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
				{
					Fail(item.Block.ObjectId, exception.Message);
				}
			}

			// Member symbols refer to an object; they do not rename that object.
			foreach (var item in items.Where(item => item.IsMember))
			{
				try
				{
					if (item.LinkedObjectId.HasValue)
					{
						var currentLocations = _mdbx.Location
							.GetByProjectId(project.Id)
							.ToDictionary(location => location.Id);

						var currentPath = ReadPath(
							item.SourceLocationId!.Value,
							currentLocations);

						var explicitlyChanged = !SamePath(
							item.RequestedLevels,
							item.OriginalLevels);

						if (explicitlyChanged &&
							!SamePath(
								RebaseParentPath(item.RequestedLevels, originalPaths),
								currentPath))
						{
							throw new InvalidOperationException(
								"The member symbol's designation conflicts with its linked object.");
						}

						resolvedObjects[item.Block.ObjectId] =
							item.LinkedObjectId.Value;
					}
					else
					{
						var levels = RebaseParentPath(
							item.RequestedLevels,
							originalPaths);

						var location = ResolveLocationPath(
							project.Id,
							levels,
							createMissing: false);

						var entity = GetContainingObject(location.Id);

						item.SourceLocationId = location.Id;
						resolvedObjects[item.Block.ObjectId] = entity.Id;
					}
				}
				catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
				{
					Fail(item.Block.ObjectId, exception.Message);
				}
			}

			// Read the final hierarchy before updating CAD presentation.
			var finalLocations = _mdbx.Location
				.GetByProjectId(project.Id)
				.ToDictionary(location => location.Id);

			var processedBlocks = 0;

			foreach (var item in items)
			{
				if (failedBlocks.Contains(item.Block.ObjectId) || !resolvedObjects.TryGetValue(item.Block.ObjectId, out var objectId))
					continue;

				try
				{
					var finalPath = ReadPath(
						item.SourceLocationId!.Value,
						finalLocations);

					RegisterSymbolLink(document.Id, sheet.Id, item.Block.ObjectId.Handle.Value, objectId);

					WriteLocationAttribute(doc, item, finalPath);

					if (item.CopiedObjectId.HasValue)
					{
						EngineeringObjectLink.ReassignCopy(
							doc.Database,
							item.Block.ObjectId,
							item.CopiedObjectId.Value,
							objectId);
					}
					else
					{
						EngineeringObjectLink.Write(
							doc.Database,
							item.Block.ObjectId,
							objectId);
					}

					processedBlocks++;
				}
				catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
				{
					Fail(item.Block.ObjectId, exception.Message);
				}
			}

			var createdLocations = finalLocations.Keys.Count(id => !originalLocations.ContainsKey(id));

			return (
				createdLocations,
				processedBlocks,
				errors.AsReadOnly());
		}

		private static string[] ParseLocation(string value)
		{
			var text = value.Trim();

			if (text.StartsWith(LocationPrefix, StringComparison.Ordinal))
				text = text.Substring(LocationPrefix.Length);

			// Membership notation points to the same location node.
			// Object membership and representation are handled separately.
			if (text.EndsWith(LevelSeparator.ToString(), StringComparison.Ordinal))
				text = text.Substring(0, text.Length - 1);

			string[] levels = text
				.Split(new[] { LevelSeparator }, StringSplitOptions.None)
				.Select(level => level.Trim())
				.ToArray();

			if (levels.Any(level => string.IsNullOrWhiteSpace(level)))
				throw new ArgumentException($"Location designation '{value}' contains an empty level.", nameof(value));

			return levels;
		}

		// TODO: Erase this method
		private static List<string> ReadLocations(AcAp.Document doc, SheetAnalysisData result)
		{
			var locations = new List<string>();

			using (var tr = doc.Database.TransactionManager.StartOpenCloseTransaction())
			{
				foreach (var item in result.Blocks)
				{
					var block = (AcDb.BlockReference)tr.GetObject(item.ObjectId, AcDb.OpenMode.ForRead);

					foreach (AcDb.ObjectId attId in block.AttributeCollection)
					{
						if (tr.GetObject(attId, AcDb.OpenMode.ForRead) is not AcDb.AttributeReference attRef)
							continue;

						if (!string.Equals(attRef.Tag,
							"BET1", // TODO: Dont use BET1
							StringComparison.OrdinalIgnoreCase))
							continue;

						if (!string.IsNullOrWhiteSpace(attRef.TextString))
							locations.Add(attRef.TextString.Trim());

						break;
					}
				}
			}
			return locations;
		}

		private void RegisterSymbolLink(Guid documentId, Guid sheetId, long blockHandle, Guid engineeringObjectId)
		{
			var existing = _mdbx.CadSymbolLink.GetByBlock(
				documentId,
				blockHandle);

			if (existing != null)
			{
				if (existing.EngineeringObjectId != engineeringObjectId)
				{
					throw new InvalidOperationException(
						"The block is already registered to another engineering object.");
				}

				if (existing.SheetId != sheetId)
				{
					throw new InvalidOperationException(
						"The CAD symbol is registered on another sheet.");
				}

				return;
			}

			Require(_mdbx.CadSymbolLink.Create(
				documentId,
				sheetId,
				blockHandle,
				engineeringObjectId));
		}

		private static string[] ReadPath(Guid locationId, IReadOnlyDictionary<Guid, ILocation> locations)
		{
			var levels = new List<string>();
			var visited = new HashSet<Guid>();
			Guid? currentId = locationId;

			while (currentId.HasValue)
			{
				if (!visited.Add(currentId.Value))
					throw new InvalidOperationException(
						"The location hierarchy contains a cycle.");

				if (!locations.TryGetValue(currentId.Value, out var location))
					throw new InvalidOperationException(
						"The location hierarchy contains a missing node.");

				levels.Add(location.Designation);
				currentId = location.ParentId;
			}

			levels.Reverse();
			return levels.ToArray();
		}

		private static bool SamePath(IEnumerable<string> first,	IEnumerable<string> second)
		{
			return first.SequenceEqual(
				second,
				StringComparer.OrdinalIgnoreCase);
		}

		private string[] RebaseParentPath(string[] requestedLevels,	IReadOnlyDictionary<Guid, string[]> originalPaths)
		{
			var parentLength = requestedLevels.Length - 1;

			// Locate the deepest known ancestor in the original parent path.
			foreach (var candidate in originalPaths
				.Where(item => item.Value.Length <= parentLength)
				.OrderByDescending(item => item.Value.Length))
			{
				if (!SamePath(
					candidate.Value,
					requestedLevels.Take(candidate.Value.Length)))
				{
					continue;
				}

				var current = _mdbx.Location.GetById(candidate.Key);

				if (current == null)
					continue;

				var currentLocations = _mdbx.Location
					.GetByProjectId(current.ProjectId)
					.ToDictionary(location => location.Id);

				return ReadPath(current.Id, currentLocations)
					.Concat(requestedLevels.Skip(candidate.Value.Length))
					.ToArray();
			}

			return requestedLevels.ToArray();
		}

		private ILocation ResolveLocationPath(Guid projectId, IReadOnlyList<string> levels,	bool createMissing)
		{
			var locations = _mdbx.Location
				.GetByProjectId(projectId)
				.ToList();

			ILocation? current = null;

			for (var depth = 0; depth < levels.Count; depth++)
			{
				var designation = levels[depth];
				var parentId = current?.Id;

				var existing = locations.SingleOrDefault(location =>
					location.Depth == depth &&
					string.Equals(
						location.Designation,
						designation,
						StringComparison.OrdinalIgnoreCase));

				if (existing != null)
				{
					if (existing.ParentId != parentId)
						throw new InvalidOperationException(
							$"Location '{designation}' already exists " +
							$"at depth {depth} beneath another parent.");

					current = existing;
					continue;
				}

				if (!createMissing)
					throw new InvalidOperationException(
						$"Location '{designation}' does not exist.");

				current = Require(_mdbx.Location.Create(
					projectId,
					designation,
					parentId));

				locations.Add(current);
			}

			return current
				?? throw new InvalidOperationException(
					"The designation contains no location levels.");
		}

		private IDocument GetOrCreateDocument(Guid projectId, string drawingPath)
		{
			var path = Path.GetFullPath(drawingPath);
			var name = Path.GetFileName(path);

			var existing = _mdbx.Document.GetAll()
				.SingleOrDefault(document =>
					document.ProjectId == projectId &&
					string.Equals(
						document.Name,
						name,
						StringComparison.OrdinalIgnoreCase) &&
					string.Equals(
						document.Path,
						path,
						StringComparison.OrdinalIgnoreCase));

			if (existing != null)
				return existing;

			var format = _pdsx.FileFormat.GetByExtension(
				Path.GetExtension(path));

			if (format == null)
			{
				throw new InvalidOperationException(
					$"No file format is registered for '{Path.GetExtension(path)}'.");
			}

			return Require(_mdbx.Document.Create(
				projectId,
				format.Id,
				name,
				path));
		}

		private ISheet GetOrCreateSheet(Guid documentId, string sheetNo)
		{
			var existing = _mdbx.Sheet.GetByDocumentId(documentId)
				.SingleOrDefault(sheet =>
					string.Equals(
						sheet.SheetNo,
						sheetNo,
						StringComparison.OrdinalIgnoreCase));

			return existing ?? Require(
				_mdbx.Sheet.Create(documentId, sheetNo));
		}

		private IEngineeringObject GetOrCreateObject(Guid projectId, Guid locationId)
		{
			// Reload because creating children or objects changes dependency versions.
			var location = _mdbx.Location.GetById(locationId)
				?? throw new InvalidOperationException(
					"The location no longer exists.");

			if (location.RepresentedByObjectId.HasValue)
			{
				var existing = _mdbx.EngineeringObject.GetById(
					location.RepresentedByObjectId.Value)
					?? throw new InvalidOperationException(
						"The location references an object that no longer exists.");

				EnsureObjectIsActive(existing);

				if (existing.ProjectId != projectId ||
					existing.LocationId != location.Id)
				{
					throw new InvalidOperationException(
						"The existing object does not match the imported location.");
				}

				return existing;
			}

			// Do not guess which unlinked object represents the location.
			if (_mdbx.EngineeringObject.GetByLocationId(location.Id).Count > 0)
			{
				throw new InvalidOperationException(
					$"Location '{location.Designation}' contains objects but has no " +
					"representing object. Resolve that association before importing.");
			}

			var entity = _mdbx.EngineeringObject.New(projectId);
			entity.LocationId = location.Id;

			var created = Require(_mdbx.EngineeringObject.Create(entity));

			// Object creation changes the location's dependency version.
			location = _mdbx.Location.GetById(location.Id)
				?? throw new InvalidOperationException(
					"The location no longer exists.");

			Require(_mdbx.Location.SetRepresentingObject(location, created.Id));

			return created;
		}

		private IEngineeringObject GetContainingObject(Guid locationId)
		{
			var location = _mdbx.Location.GetById(locationId)
				?? throw new InvalidOperationException(
					"The location no longer exists.");

			if (!location.RepresentedByObjectId.HasValue)
			{
				throw new InvalidOperationException(
					$"Location '{location.Designation}' has no representing object. " +
					"Import its main symbol before its member symbols.");
			}

			var entity = _mdbx.EngineeringObject.GetById(
				location.RepresentedByObjectId.Value)
				?? throw new InvalidOperationException(
					"The representing object no longer exists.");

			EnsureObjectIsActive(entity);

			return entity;
		}

		/// <summary>
		/// Resolves the location represented by an existing engineering object.
		/// </summary>
		private ILocation GetRepresentedLocation(IEngineeringObject entity, Guid projectId)
		{
			EnsureObjectIsActive(entity);

			if (entity.ProjectId != projectId)
				throw new InvalidOperationException("The engineering object belongs to another project.");

			if (!entity.LocationId.HasValue)
				throw new InvalidOperationException("The engineering object has no location membership.");

			var location = _mdbx.Location.GetById(entity.LocationId.Value)
				?? throw new InvalidOperationException("The object's location no longer exists.");

			if (location.ProjectId != projectId || location.RepresentedByObjectId != entity.Id)
				throw new InvalidOperationException("The engineering object does not represent its assigned location.");

			return location;
		}

		private static void WriteLocationAttribute(AcAp.Document doc, ImportBlock item,	IReadOnlyList<string> levels)
		{
			var originalText = item.Attribute.Value;
			var trimmed = originalText.Trim();

			var prefix = trimmed.StartsWith(
				LocationPrefix,
				StringComparison.Ordinal)
					? LocationPrefix
					: string.Empty;

			var newText =
				prefix +
				string.Join(LevelSeparator.ToString(), levels) +
				(item.IsMember ? LevelSeparator.ToString() : string.Empty);

			if (string.Equals(originalText, newText, StringComparison.Ordinal))
				return;

			using var transaction = doc.Database.TransactionManager
				.StartTransaction();

			var attribute = transaction.GetObject(
				item.Attribute.ObjectId,
				AcDb.OpenMode.ForRead) as AcDb.AttributeReference
				?? throw new InvalidOperationException(
					"The location attribute no longer exists.");

			if (!string.Equals(
				attribute.TextString,
				originalText,
				StringComparison.Ordinal))
			{
				throw new InvalidOperationException(
					"The location attribute changed during analysis. " +
					"The database may already have been updated; run the analysis again.");
			}

			attribute.UpgradeOpen();
			attribute.TextString = newText;

			transaction.Commit();
		}

		/// <summary>
		/// Applies a requested location path while preserving object identity.
		/// </summary>
		private (ILocation? Value, ValidationResult Validation) UpdateLinkedLocation(
			Guid objectId,
			Guid projectId,
			IReadOnlyList<string> targetLevels)
		{
			var entity = _mdbx.EngineeringObject.GetById(objectId);

			if (entity == null)
			{
				return (null, new ValidationResult(new[]
				{
			new ValidationFailure(
				nameof(objectId),
				"The engineering object referenced by XData no longer exists.")
		}));
			}

			if (entity.ProjectId != projectId)
			{
				return (null, new ValidationResult(new[]
				{
			new ValidationFailure(
				nameof(entity.ProjectId),
				"The engineering object belongs to another project.")
		}));
			}

			var location = GetRepresentedLocation(entity, projectId);

			return _mdbx.Location.ChangePath(location, targetLevels);
		}

		private static void EnsureObjectIsActive(IEngineeringObject entity)
		{
			if (entity.IsMarkedForDeletion)
			{
				throw new InvalidOperationException(
					$"Engineering object '{entity.Id}' is marked for deletion. " +
					"The CAD symbol has been preserved. " +
					"Restore the object or explicitly confirm removal of the symbol.");
			}
		}

		private sealed class ImportBlock
		{
			internal ImportBlock(CadBlock block, CadAttribute attribute, string[] requestedLevels, bool isMember)
			{
				Block = block;
				Attribute = attribute;
				RequestedLevels = requestedLevels;
				IsMember = isMember;
			}

			internal CadBlock Block { get; }

			internal CadAttribute Attribute { get; }

			internal string[] RequestedLevels { get; }

			internal bool IsMember { get; }

			internal Guid? LinkedObjectId { get; set; }

			/// <summary>
			/// Gets or sets the object identifier inherited by a copied main symbol.
			/// Null indicates that the block has not been classified as a copy.
			/// </summary>
			internal Guid? CopiedObjectId { get; set; }

			internal Guid? SourceLocationId { get; set; }

			internal string[] OriginalLevels { get; set; } = Array.Empty<string>();
		}

		private static T Require<T>((T? Value, ValidationResult Validation) result) where T : class
		{
			if (!result.Validation.IsValid)
			{
				throw new InvalidOperationException(
					string.Join(
						Environment.NewLine,
						result.Validation.Errors
							.Select(error => error.ErrorMessage)
							.Distinct()));
			}

			return result.Value
				?? throw new InvalidOperationException(
					"The operation succeeded without returning a value.");
		}
	}
}
