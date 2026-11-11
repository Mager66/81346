using System;
using System.Collections.Generic;
using System.Linq;
using DevExpress.Xpo;
using ELPROCAD.API.Contracts;
using ELPROCAD.API.Model;

namespace ELPROCAD.API.Services
{
	public sealed class DocumentReader : IDocumentReader
	{
		private readonly UnitOfWork _unitOfWork;

		public DocumentReader(UnitOfWork unitOfWork)
		{
			_unitOfWork = unitOfWork
				?? throw new ArgumentNullException(nameof(unitOfWork));
		}

		public IReadOnlyList<DocumentDto>? GetByProject(Guid projectId)
		{
			var project = _unitOfWork.GetObjectByKey<Project>(projectId);

			if (project == null)
				return null;

			var documents = new XPQuery<Document>(_unitOfWork)
				.Where(document => document.Project == project)
				.OrderBy(document => document.Name)
				.ToList();

			return documents.Select(document => new DocumentDto
			{
				Id = document.Id,
				ProjectId = project.Id,
				Name = document.Name
			}).ToList().AsReadOnly();
		}
	}
}