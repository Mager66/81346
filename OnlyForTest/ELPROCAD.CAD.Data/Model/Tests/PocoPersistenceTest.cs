using DevExpress.Xpo;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELPROCAD.CAD.Data.Model.Tests
{
	public sealed class PocoPersistenceTest
	{
		private readonly IServiceScopeFactory _scopeFactory;

		public PocoPersistenceTest(IServiceScopeFactory serviceScopeFactory) => 
			_scopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));

		public string Run()
		{
			var created = new PocoTest
			{
				Name = "POCO-test",
				Description = "Skapad utan session"
			};

			// 1. Create table and store the new object
			using(var scope = _scopeFactory.CreateScope())
			{
				var uow = scope.ServiceProvider.GetRequiredService<UnitOfWork>();

				uow.UpdateSchema(typeof(PocoTest));
				uow.Save(created);
				uow.CommitChanges();
			}

			PocoTest loaded;

			// 2. Read back with a new session
			using(var scope = _scopeFactory.CreateScope())
			{
				var uow = scope.ServiceProvider.GetRequiredService<UnitOfWork>();

				loaded = uow.GetObjectByKey<PocoTest>(created.Id)
					?? throw new InvalidOperationException(
						"Objektet kunde inte läsas tillbaka.");
			}

			// 3. Do some changes after loading session is closed
			loaded.Name = "Ändrad utan session";
			loaded.Description = "Ska sparas med en ny session";

			// 4. Try to save the same object with a new session
			using(var scope = _scopeFactory.CreateScope())
			{
				var uow = scope.ServiceProvider.GetRequiredService<UnitOfWork>();

				var existing = uow.GetObjectByKey<PocoTest>(loaded.Id)
					?? throw new InvalidOperationException("Testposten saknas.");

				existing.Name = loaded.Name;
				existing.Description = loaded.Description;

				uow.Save(existing);
				uow.CommitChanges();
			}
			
			// 5. Compare and test new values
			using(var scope = _scopeFactory.CreateScope())
			{
				var uow = scope.ServiceProvider.GetRequiredService<UnitOfWork>();

				var saved = uow.GetObjectByKey<PocoTest>(created.Id)
					?? throw new InvalidOperationException(
						"Objektet saknas efter uppdateringen.");

				if (saved.Name != loaded.Name || saved.Description != loaded.Description)
					throw new InvalidOperationException("Ändringarna sparades inte korrekt.");

				return $"OK: {saved.Id}\n{saved.Name}\n{saved.Description}";
			}
		}

		public string RunRelation()
		{
			var parentId = Guid.NewGuid();
			var childId = Guid.NewGuid();

			// 1. Skapa och spara båda i samma session.
			using (var scope = _scopeFactory.CreateScope())
			{
				var unitOfWork = scope.ServiceProvider
					.GetRequiredService<UnitOfWork>();

				unitOfWork.UpdateSchema(
					typeof(PocoTest),
					typeof(PocoTestChild));

				var parent = new PocoTest
				{
					Id = parentId,
					Name = "Förälder",
					Description = "Relationstest"
				};

				var child = new PocoTestChild
				{
					Id = childId,
					Name = "Barn",
					Parent = parent
				};

				unitOfWork.Save(parent);
				unitOfWork.Save(child);
				unitOfWork.CommitChanges();
			}

			PocoTestChild loaded;

			// 2. Läs tillbaka och kontrollera referensen.
			using (var scope = _scopeFactory.CreateScope())
			{
				var unitOfWork = scope.ServiceProvider
					.GetRequiredService<UnitOfWork>();

				loaded = unitOfWork.GetObjectByKey<PocoTestChild>(childId)
					?? throw new InvalidOperationException("Barnet saknas.");

				if (loaded.Parent == null || loaded.Parent.Id != parentId)
					throw new InvalidOperationException("Föräldrareferensen är fel.");
			}

			// 3. Läs och ändra efter att sessionen stängts.
			var parentName = loaded.Parent.Name;
			loaded.Name = "Ändrat barn utan session";

			// 4. Hämta instanserna i sparningssessionen.
			using (var scope = _scopeFactory.CreateScope())
			{
				var unitOfWork = scope.ServiceProvider
					.GetRequiredService<UnitOfWork>();

				var existing = unitOfWork.GetObjectByKey<PocoTestChild>(childId)
					?? throw new InvalidOperationException("Barnet saknas.");

				var parent = unitOfWork.GetObjectByKey<PocoTest>(loaded.Parent.Id)
					?? throw new InvalidOperationException("Föräldern saknas.");

				existing.Name = loaded.Name;
				existing.Parent = parent;

				unitOfWork.Save(existing);
				unitOfWork.CommitChanges();
			}

			// 5. Verifiera med en ny session.
			using (var scope = _scopeFactory.CreateScope())
			{
				var unitOfWork = scope.ServiceProvider
					.GetRequiredService<UnitOfWork>();

				var saved = unitOfWork.GetObjectByKey<PocoTestChild>(childId)
					?? throw new InvalidOperationException("Barnet saknas.");

				if (saved.Name != loaded.Name ||
					saved.Parent == null ||
					saved.Parent.Id != parentId)
				{
					throw new InvalidOperationException(
						"Namnet eller relationen sparades inte korrekt.");
				}

				return $"OK: {saved.Name} → {parentName}";
			}
		}

		public string RunConcurrency()
		{
			var id = Guid.NewGuid();

			using(var scope = _scopeFactory.CreateScope())
			{
				var unitOfWork = scope.ServiceProvider.GetRequiredService<UnitOfWork>();

				unitOfWork.UpdateSchema(typeof(PocoTest));

				unitOfWork.Save(new PocoTest
				{
					Id = id,
					Name = "Ursprungligt värde",
					Description = "Test av samtidiga ändringar"
				});
				unitOfWork.CommitChanges();			
			}

			bool conflictDetected = false;

			using (var firstScope  = _scopeFactory.CreateScope())
				using(var secondScope  = _scopeFactory.CreateScope())
			{
				var first = firstScope.ServiceProvider.GetRequiredService<UnitOfWork>();

				var second = secondScope.ServiceProvider.GetRequiredService<UnitOfWork>();

				first.LockingOption = LockingOption.Optimistic;
				second.LockingOption = LockingOption.Optimistic;

				// Both reads the same version inside this wrapper
				var firstObject=first.GetObjectByKey<PocoTest>(id)
					?? throw new InvalidOperationException("Testposten saknas.");

				var secondObject = second.GetObjectByKey<PocoTest>(id)
					?? throw new InvalidOperationException("Testposten saknas.");

				firstObject.Name = "Sparat av användare 1";
				first.Save(firstObject);
				first.CommitChanges();

				// The other session still has the old version
				secondObject.Name = "Sparat av användare 2";

				try
				{
					second.Save(secondObject);
					second.CommitChanges();
				}
				catch(DevExpress.Xpo.DB.Exceptions.LockingException)
				{
					conflictDetected = true;
				}

				// Check that the first users values still remains
				using(var scope  = _scopeFactory.CreateScope())
				{
					var uow = scope.ServiceProvider.GetRequiredService<UnitOfWork>();

					var saved = uow.GetObjectByKey<PocoTest>(id)
						?? throw new InvalidOperationException("Testposten saknas.");

					if (saved.Name != "Sparat av användare 1")
					{
						throw new InvalidOperationException(
							"Första användarens ändring har skrivits över.");
					}
				}
				return "OK: Andra sparningen stoppades. Första ändringen finns kvar.";
			}
		}
	}
}
