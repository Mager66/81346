using DevExpress.Xpo;
using ELPROCAD.CAD.Data.Model.Classification;
using ELPROCAD.CAD.Data.Validation.Classification;
using FluentValidation;
using FluentValidation.Results;

namespace ELPROCAD.CAD.Data.CRUD.Classification
{
	public sealed class QuerySubClassification
	{
		//private readonly UnitOfWork _unitOfWork;
		//private readonly SubClassValidator? _validator;

		//public QuerySubClassification(UnitOfWork unitOfWork) => _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

		//public (SubClass? Value, ValidationResult Validation) Create(Guid mainClassId, string abbrevation, string tooltip, string image)
		//{
		//	var main = new XPQuery<MainClass>(_unitOfWork).FirstOrDefault(x => x.Id == mainClassId);
		//	if(main == null)
		//	{
		//		return (null, new ValidationResult(new[]
		//		{
		//			new ValidationFailure(nameof(mainClassId),
		//			"Huvudklassen finns inte.")
		//		}));
		//	}

		//	var existing = new XPQuery<SubClass>(_unitOfWork)
		//		.FirstOrDefault(x =>
		//		x.MainClass == main &&
		//		x.Abbrevation == abbrevation);

		//	if (existing != null)
		//		return (existing, new ValidationResult());

		//	var sub = new SubClass(_unitOfWork)
		//	{
		//		Abbrevation = abbrevation,
		//		Tooltip = tooltip,
		//		Image = image,
		//		MainClass = main
		//	};

		//	_unitOfWork.CommitChanges();
		//	return (sub, new ValidationResult());
		//}

		//public SubClass? GetById(Guid subClassId) => new XPQuery<SubClass>(_unitOfWork).FirstOrDefault(x => x.Id == subClassId);

		//public IReadOnlyList<SubClass> GetAll(Guid mainClassId) => 
		//	new XPQuery<SubClass>(_unitOfWork)
		//	.Where(x=>x.MainClass.Id  == mainClassId)
		//	.OrderBy(c => c.Abbrevation).ToList().AsReadOnly();

		//public (SubClass?, ValidationResult Validation) Update(Guid id, string abbrevation, string tooltip, string image)
		//{
		//	var sub = GetById(id);
		//	if(sub == null)
		//	{
		//		return (null, new ValidationResult(new[]
		//		{
		//			new ValidationFailure(nameof(id),
		//			"Subklassen finns inte.")
		//		}));
		//	}

		//	sub.Abbrevation = abbrevation;
		//	sub.Tooltip = tooltip;
		//	sub.Image = image;

		//	_unitOfWork.CommitChanges();

		//	return (sub, new ValidationResult());
		//}

		//public ValidationResult Delete(Guid id)
		//{
		//	var sub = GetById(id);
		//	if (sub == null)
		//	{
		//		return new ValidationResult(new[]
		//		{
		//			new ValidationFailure(nameof(SubClass.Id),
		//			"Subklassen finns inte.")
		//		});
		//	}

		//	sub.Delete();
		//	_unitOfWork.CommitChanges();

		//	return new ValidationResult();
		//}
	}
}
