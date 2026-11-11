using DevExpress.Xpo;
using ELPROCAD.CAD.Data.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELPROCAD.CAD.Data.CRUD.Analisys
{
	public sealed class QueryEngineeringObject
	{
		private readonly UnitOfWork _unitOfWork;

		public QueryEngineeringObject(UnitOfWork unitOfWork) => _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

		public EngineeringObject? GetObjectById(Guid id) => _unitOfWork.GetObjectByKey<EngineeringObject>(id);

		public IReadOnlyList<EngineeringObject> GetAll()
		{
			return new XPQuery<EngineeringObject>(_unitOfWork)
			//.OrderBy(d => d.Name)
			.ToList()
			.AsReadOnly();
		}

		/// <summary>
		/// Adds like this when object is coming from analize in CAD
		/// </summary>
		/// <param name="sheetId"></param>
		/// <param name="locationName"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentNullException"></exception>
		public EngineeringObject AddFromAnalize(Project project, Guid sheetId, string location = "", string function = "", string product = "")
		{
			var sheet = _unitOfWork.GetObjectByKey<Sheet>(sheetId);
			if (sheet == null)
				throw new ArgumentNullException("Kan inte skapa blad utan dokument");

			var eObject = new EngineeringObject(_unitOfWork)
			{
				//Name = location
				Project = project,
				Sheet= sheet
			};
			project.Objects.Add(eObject);
			sheet.Objects.Add(eObject);

			if (string.IsNullOrWhiteSpace(location) && string.IsNullOrWhiteSpace(function) && string.IsNullOrWhiteSpace(product))
				throw new ArgumentException("Nåt jäkla namn och tillhörighet måste den ha :D");

			if(!string.IsNullOrWhiteSpace(location))			
				eObject.Location = GetOrCreateLocation(location);
			

			_unitOfWork.CommitChanges();

			return eObject;
		}

		/// <summary>
		/// Adds like this when object is coming from analize in CAD
		/// </summary>
		/// <param name="sheetId"></param>
		/// <param name="locationName"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentNullException"></exception>


		public EngineeringObject Update(Guid id, Guid sheetId)
		{
			var eObject = GetObjectById(id)
				?? throw new KeyNotFoundException(
					$"Projektet '{id}' finns inte.");

			var sheet = _unitOfWork.GetObjectByKey<Sheet>(sheetId);

			eObject.Sheet = sheet;

			_unitOfWork.CommitChanges();

			return eObject;
		}

		//public bool Delete(Guid id)
		//{
		//	var eObject = GetObjectById(id);

		//	if (eObject == null)
		//		return false;

		//	var hasObjects = new XPQuery<EngineeringObject>(_unitOfWork)
		//		.Any(o => o.Sheet == eObject);

		//	if (hasObjects)
		//		throw new InvalidOperationException("Bladet innehåller objekt.");

		//	_unitOfWork.Delete(eObject);
		//	_unitOfWork.CommitChanges();

		//	return true;
		//}

		#region Private helpers

		private Location GetOrCreateLocation(string location)
		{
			var levels = location.Split('.');

			if(levels.Any(level => string.IsNullOrWhiteSpace(level)))
				throw new ArgumentException("Placeringen innehåller en tom nivå.", nameof(location));

			var locations = new XPQuery<Location>(_unitOfWork).ToList();

			Location? parent = null;

			for(var depth = 0; depth < levels.Length;  depth++)
			{
				var name = levels[depth].Trim();

				var matches = locations
					.Where(item=>
					string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)
					&& GetDepth(item) == depth)
					.ToList();

				if (matches.Count > 1)
					throw new InvalidOperationException($"Placeringen '{name}' förekommer flera gånger på nivå {depth}.");

				var node = matches.SingleOrDefault();

				if(node != null)
				{
					if (node.Parent != parent)
						throw new InvalidOperationException($"Placeringen '{name}' finns redan på nivå {depth} under en annan parent.");
				}
				else
				{
					node = new Location(_unitOfWork)
					{
						Name = name,
						Parent = parent
					};
					locations.Add(node);
				}
				parent = node;
			}
			return parent!;
		}

		private static int GetDepth(Location location)
		{
			var depth = 0;
			var visited=new HashSet<Location>();

			for(var current  = location; current != null; current = current.Parent)
			{
				if(!visited.Add(current))
					throw new InvalidOperationException("Placeringsstrukturen innehåller en cirkel.");

				if(current.Parent != null)
					depth++;
			}
			return depth;
		}



		#endregion Private helpers
	}
}
