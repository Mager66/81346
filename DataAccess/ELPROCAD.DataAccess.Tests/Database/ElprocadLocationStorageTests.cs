using ELPROCAD.Analysis.Configuration;
using ELPROCAD.Analysis.Extraction;
using ELPROCAD.DataAccess.Contracts.MDBX.Aspects;
using ELPROCAD.DataAccess.Repositories.MDBX;
using Xunit;

namespace ELPROCAD.DataAccess.Tests.Database
{
	/// <summary>Verifies the extraction-to-storage contract against a real MDBX database.</summary>
	public sealed class ElprocadLocationStorageTests : IDisposable
	{
		private readonly string _directory = Path.Combine(Path.GetTempPath(), "ELPROCAD.FlatLocationTests", Guid.NewGuid().ToString("N"));
		private readonly Guid _projectId = Guid.NewGuid();
		private readonly IMdbxRepository _mdbx;
		private readonly IElprocadObjectExtractor _extractor = ExtractionProfiles.LoadElprocadJson("""
        {
          "schemaVersion": 1, "profileId": "ELPROCAD", "extractor": "ELPROCAD",
          "boundaries": "Ignore", "activeAspects": ["Location"],
          "location": { "structure": "Flat", "placementAttribute": "PLAC", "designationAttribute": "BET1", "separator": "." }
        }
        """);

		public ElprocadLocationStorageTests() =>
			_mdbx = new DataAccessInitializer().InitializeMdbx(Path.Combine(_directory, "Project.mdbx"));

		[Fact(DisplayName = "ELPROCAD stores a combined designation as a root without creating a placement parent")]
		public void FlatLocation_IsStoredAsRoot()
		{
			var location = Store("AS1", "Q02");
			Assert.Equal("AS1.Q02", location.Designation);
			Assert.Null(location.ParentId);
			Assert.Equal(0, location.Depth);
			Assert.Single(_mdbx.Location.GetByProjectId(_projectId));
		}

		[Fact(DisplayName = "Objects sharing PLAC remain independent flat Location entries")]
		public void SharedPlacement_DoesNotCreateHierarchy()
		{
			Store("AS1", "Q02"); Store("AS1", "Q03"); Store("AS2", "Q02");
			var locations = _mdbx.Location.GetByProjectId(_projectId);
			Assert.Equal(3, locations.Count);
			Assert.All(locations, x => { Assert.Null(x.ParentId); Assert.Equal(0, x.Depth); });
			Assert.DoesNotContain(locations, x => x.Designation == "AS1" || x.Designation == "AS2");
		}

		[Fact(DisplayName = "Repeated flat extraction resolves the existing Location identifier")]
		public void Reanalysis_ReusesLocation()
		{
			var first = Store("AS1", "Q02");
			Assert.Equal(first.Id, Store("AS1", "Q02").Id);
			Assert.Single(_mdbx.Location.GetByProjectId(_projectId));
		}

		[Fact(DisplayName = "A missing placement produces no Location writes")]
		public void MissingPlacement_DoesNotStoreLocation()
		{
			Assert.Throws<InvalidOperationException>(() => Store("", "Q02"));
			Assert.Empty(_mdbx.Location.GetByProjectId(_projectId));
		}

		[Fact(DisplayName = "Changing a flat object's placement does not rename another object with the same PLAC")]
		public void ChangePath_RemainsFlatAndIndependent()
		{
			var first = Store("AS1", "Q02");
			var second = Store("AS1", "Q03");
			var levels = Levels("AS2", "Q02");
			var result = _mdbx.Location.ChangePath(_mdbx.Location.GetById(first.Id)!, levels);
			Assert.True(result.Validation.IsValid, string.Join("; ", result.Validation.Errors.Select(x => x.ErrorMessage)));
			Assert.Equal(first.Id, result.Value!.Id);
			Assert.Equal("AS2.Q02", result.Value.Designation);
			Assert.Null(result.Value.ParentId);
			Assert.Equal("AS1.Q03", _mdbx.Location.GetById(second.Id)!.Designation);
			Assert.Equal(2, _mdbx.Location.GetByProjectId(_projectId).Count);
		}

		[Fact(DisplayName = "A configured source mapping is exposed to the CAD adapter")]
		public void Adapter_UsesConfiguredSourceTags()
		{
			Assert.Equal("PLAC", _extractor.PlacementAttributeTag);
			Assert.Equal("BET1", _extractor.DesignationAttributeTag);
		}

		private IReadOnlyList<string> Levels(string placement, string designation)
		{
			var result = _extractor.Extract(new (string Tag, string? Value)[] { ("PLAC", placement), ("BET1", designation) });
			if (!result.IsValid) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Message)));
			var aspect = Assert.Single(result.Aspects);
			Assert.Equal(AspectStructure.Flat, aspect.Structure);
			Assert.Single(aspect.Levels);
			return aspect.Levels;
		}

		private ILocation Store(string placement, string designation)
		{
			var name = Levels(placement, designation).Single();
			var existing = _mdbx.Location.GetByProjectId(_projectId)
				.SingleOrDefault(x => x.ParentId == null && string.Equals(x.Designation, name, StringComparison.OrdinalIgnoreCase));
			if (existing != null) return existing;
			var result = _mdbx.Location.Create(_projectId, name, null);
			Assert.True(result.Validation.IsValid, string.Join("; ", result.Validation.Errors.Select(x => x.ErrorMessage)));
			return result.Value!;
		}

		public void Dispose()
		{
			if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
		}
	}
}
