using DevExpress.Xpo;
using ELPROCAD.CAD.Data.Model;
using ELPROCAD.CAD.Data.Model.Classification;
using ELPROCAD.CAD.Data.Provider;
using ELPROCAD.CAD.Data.Validation.Classification;
using ELPROCAD.CAD.Data.Validation.VistDb;
using FluentValidation.Results;

namespace ELPROCAD.CAD.Data.CRUD.Classification
{
	public sealed class QueryMainClassification
	{
		private readonly DatabaseAccess _database;
		private readonly MainClassValidator _validator = new();
		private readonly Action? _beforePersist;

		public QueryMainClassification(DatabaseAccess database) : this(database, null) { }

		internal QueryMainClassification(DatabaseAccess database, Action? beforePersist)
		{
			_database = database
				?? throw new ArgumentNullException(nameof(database));

			_beforePersist = beforePersist;
		}

		//public QueryMainClassification(DatabaseAccess database) => 
		//	_database = database ?? throw new ArgumentNullException(nameof(database));

		public MainClass? GetById(Guid mainClassId)
		{
			return _database.Execute(uow =>
			{
				var main = uow.GetObjectByKey<MainClass>(mainClassId);

				if (main != null)
					CaptureVersion(uow, main);

				return main;
			});
		}

		public IReadOnlyList<MainClass> GetAll()
		{
			return _database.Execute<IReadOnlyList<MainClass>>(uow =>
			{
				var items = new XPQuery<MainClass>(uow)
				.OrderBy(x => x.Abbrevation)
				.ToList();

				foreach (var item in items)
					CaptureVersion(uow, item);

				return items.AsReadOnly();
			});
		}

		public (MainClass? Value, ValidationResult Validation) Create(
			string abbrevation, string tooltip, string image) =>
			Save(null, abbrevation, tooltip, image, null);

		public (MainClass? Value, ValidationResult Validation) Update(
			Guid id, string abbrevation, string tooltip, string image, int? loadedVersion)
		{
			if (!loadedVersion.HasValue)
				return (null, Failure(string.Empty, "Postens ursprungliga version saknas. Läs in posten igen."));

			return Save(id, abbrevation, tooltip, image, loadedVersion);
		}

		private (MainClass? Value, ValidationResult Validation) Save(
		Guid? id, string abbrevation, string tooltip, string image, int? loadedVersion)
		{
			return _database.Execute<(MainClass? value, ValidationResult Validation)>(
				uow =>
				{
					MainClass main;

					if (id.HasValue)
					{
						var existing = uow.GetObjectByKey<MainClass>(id.Value);

						if (existing == null)
							return (null, Failure(nameof(MainClass.Id), "Huvudklassen finns inte."));

						// Check concurrency before new values are written
						if (GetVersion(uow, existing) != loadedVersion)
							return (null, Failure(string.Empty, "Posten har ändrats av någon annan. " +
								"Dina ändringar har inte sparats. Läs in senaste versionen."));

						main = existing;
					}
					else
					{
						main = new MainClass();
					}

					// Keaps behavior: Create returns existing if abbrevation exists
					var duplicate = new XPQuery<MainClass>(uow)
					.FirstOrDefault(x =>
					x.Abbrevation == abbrevation && x.Id != main.Id);

					if (duplicate != null)
					{					
						return (null, Failure(nameof(MainClass.Abbrevation), "Förkortningen används redan av en annan huvudklass."));
					}

					main.Abbrevation = abbrevation;
					main.Tooltip = tooltip;
					main.Image = image;

					var validation = _validator.Validate(main);
					main.ApplyValidationResult(validation);

					if (!validation.IsValid)
						return (null, validation);

					// POCO: Save explicit the instance belonging to this operation
					try
					{
						_beforePersist?.Invoke();

						uow.Save(main);
						uow.CommitChanges();
					}
					catch (DevExpress.Xpo.DB.Exceptions.LockingException)
					{
						// Någon hann ändra efter vår versionskontroll.
						return (null, Failure(
							string.Empty,
							"Posten ändrades av någon annan under sparningen. " +
							"Dina ändringar har inte sparats. Läs in senaste versionen."));
					}
					catch(Exception ex) when(VistaDbErrors.IsDuplicateKey(ex))
					{
						return (null, Failure(string.Empty,
							"Posten kunde inte sparas eftersom en unik nyckel redan finns. " +
							"Förkortningen kan ha registrerats av någon annan. Läs in senaste data."));
					}

					CaptureVersion(uow, main);

					return (main, validation);
				});
		}

		public ValidationResult Delete(Guid id)
		{
			return _database.Execute(uow =>
			{
				var main = uow.GetObjectByKey<MainClass>(id);

				if (main == null)
					return Failure(nameof(MainClass.Abbrevation), "Huvudklassen finns inte.");

				// Query db: The sessionfree subclass-list dows not have to be loaded
				var hasSubClasses = new XPQuery<SubClass>(uow).Any(x => x.MainClass == main);
				if (hasSubClasses)
					return Failure(nameof(MainClass.SubClasses), "Huvudklassen kan inte raderas eftersom den har underklasser.");

				uow.Delete(main);
				uow.CommitChanges();

				return new ValidationResult();
			});
		}

		private static ValidationResult Failure(string propertyName, string message) => new ValidationResult(new[]
			{
				new ValidationFailure(propertyName, message)
			});

		private static int GetVersion(UnitOfWork uow, MainClass main)
		{
			var member = uow.GetClassInfo(typeof(MainClass))
				.GetMember("OptimisticLockField");

			if (member == null)
				throw new InvalidOperationException("MainClass saknar OptimisticLockField.");

			return Convert.ToInt32(member.GetValue(main));
		}

		private static void CaptureVersion(UnitOfWork uow, MainClass main) => main.LoadedVersion = GetVersion(uow, main);
	}
}
