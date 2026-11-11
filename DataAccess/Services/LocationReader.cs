using System;
using System.Collections.Generic;
using System.Linq;
using DevExpress.Xpo;
using ELPROCAD.API.Contracts;
using ELPROCAD.API.Model;

namespace ELPROCAD.API.Services
{
	public sealed class LocationReader : ILocationReader
	{
		private readonly UnitOfWork _unitOfWork;

		public LocationReader(UnitOfWork unitOfWork)
		{
			_unitOfWork = unitOfWork
				?? throw new ArgumentNullException(nameof(unitOfWork));
		}

		public IReadOnlyList<LocationDto> GetAll()
		{
			var locations = new XPQuery<Location>(_unitOfWork)
				.OrderBy(location => location.Name)
				.ToList();

			return locations.Select(location => new LocationDto
			{
				Id = location.Id,
				Name = location.Name,
				ParentId = location.Parent?.Id,
				RepresentingObjectId = location.RepresentingObject?.Id
			}).ToList().AsReadOnly();
		}
	}
}