using System;
using ELPROCAD.DataAccess.Changes.Locations;
using ELPROCAD.DataAccess.Contracts.MDBX.Aspects;
using ELPROCAD.DataAccess.Models.MDBX.Aspects;
using Xunit;

namespace ELPROCAD.DataAccess.Tests.Analysis
{
	public sealed class LocationChangePlannerTests
	{
		private readonly LocationChangePlanner _planner = new();

		[Theory(DisplayName =
		"A designation change is classified without modifying the hierarchy")]
		[InlineData("AS1", "B3", "Q02", 0)]
		[InlineData("AS1", "B3", "Q04", 1)]
		[InlineData("AS1", "B4", "Q02", 2)]
		[InlineData("AS1", "B4", "Q04", 3)]
		public void Plan_ClassifiesChange(
		string rootName,
		string parentName,
		string designation,
		int expected)
		{
			var projectId = Guid.NewGuid();

			var root = new Location
			{
				ProjectId = projectId,
				Designation = "AS1",
				Depth = 0
			};

			var parent = new Location
			{
				ProjectId = projectId,
				Designation = "B3",
				ParentId = root.Id,
				Depth = 1
			};

			var source = new Location
			{
				ProjectId = projectId,
				Designation = "Q02",
				ParentId = parent.Id,
				Depth = 2
			};

			var sibling = new Location
			{
				ProjectId = projectId,
				Designation = "Q03",
				ParentId = parent.Id,
				Depth = 2
			};

			ILocation[] locations = { root, parent, source, sibling };

			var result = _planner.Plan(
				source,
				new[] { rootName, parentName, designation },
				locations);

			Assert.Equal((LocationChangeKind)expected, result.Kind);
			Assert.Equal(source.Id, result.LocationId);
			Assert.Equal(designation, result.Designation);
			Assert.Equal(
				new[] { rootName, parentName },
				result.ParentLevels);

			Assert.Equal("Q02", source.Designation);
			Assert.Equal(parent.Id, source.ParentId);
			Assert.Equal(parent.Id, sibling.ParentId);
		}
	}
}