using System;
using System.Collections.Generic;
using System.Linq;
using DevExpress.Xpo;
using ELPROCAD.API.Contracts;
using ELPROCAD.API.Model;

namespace ELPROCAD.API.Services
{
	public sealed class FunctionReader : IFunctionReader
	{
		private readonly UnitOfWork _unitOfWork;

		public FunctionReader(UnitOfWork unitOfWork)
		{
			_unitOfWork = unitOfWork
				?? throw new ArgumentNullException(nameof(unitOfWork));
		}

		public IReadOnlyList<FunctionDto> GetAll()
		{
			var functions = new XPQuery<Function>(_unitOfWork)
				.OrderBy(function => function.Name)
				.ToList();

			return functions.Select(function => new FunctionDto
			{
				Id = function.Id,
				Name = function.Name,
				ParentId = function.Parent?.Id,
				RepresentingObjectId = function.RepresentingObject?.Id
			}).ToList().AsReadOnly();
		}
	}
}