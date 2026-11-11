using ELPROCAD.DataAccess.Models.MDBX.Aspects;
using ELPROCAD.DataAccess.Models.MDBX.Objects;
using ELPROCAD.DataAccess.Validation;
using Xunit;

namespace ELPROCAD.DataAccess.Tests.Validation
{
	public sealed class EngineeringObjectValidationTests
	{
		private readonly IValidationService _validation =
			new ValidationService();

		[Theory(DisplayName = "An object may exist without a sheet when any one aspect is assigned")]
		[InlineData("PowerFunction")]
		[InlineData("ControlFunction")]
		[InlineData("TopographicStructure")]
		[InlineData("Location")]
		[InlineData("Function")]
		[InlineData("Product")]
		public void OneAspectWithoutSheet_IsAccepted(string aspect)
		{
			var entity = new EngineeringObject
			{
				ProjectId = Guid.NewGuid()
			};

			var nodeId = Guid.NewGuid();

			switch (aspect)
			{
				case "PowerFunction":
					entity.PowerFunctionId = nodeId;
					break;
				case "ControlFunction":
					entity.ControlFunctionId = nodeId;
					break;
				case "TopographicStructure":
					entity.TopographicStructureId = nodeId;
					break;
				case "Location":
					entity.LocationId = nodeId;
					break;
				case "Function":
					entity.FunctionId = nodeId;
					break;
				case "Product":
					entity.ProductId = nodeId;
					break;
			}

			Assert.Null(entity.SheetId);
			Assert.True(_validation.Validate(entity).IsValid);
		}

		[Theory(DisplayName = "An object without an aspect is rejected even when it belongs to a sheet")]
		[InlineData(false)]
		[InlineData(true)]
		public void NoAspect_IsRejected(bool hasSheet)
		{
			var entity = new EngineeringObject
			{
				ProjectId = Guid.NewGuid(),
				SheetId = hasSheet ? Guid.NewGuid() : null
			};

			var result = _validation.Validate(entity);

			Assert.False(result.IsValid);
			Assert.Single(result.Errors);
		}

		[Fact(DisplayName =	"An object can belong to multiple different aspects")]
		public void MultipleAspects_AreAccepted()
		{
			var entity = new EngineeringObject
			{
				ProjectId = Guid.NewGuid(),
				LocationId = Guid.NewGuid(),
				FunctionId = Guid.NewGuid(),
				ProductId = Guid.NewGuid()
			};

			Assert.True(_validation.Validate(entity).IsValid);
		}

		[Fact(DisplayName =	"An engineering object requires a project")]
		public void MissingProject_IsRejected()
		{
			var entity = new EngineeringObject
			{
				LocationId = Guid.NewGuid()
			};

			var result = _validation.Validate(entity);
			var error = Assert.Single(result.Errors);

			Assert.Equal(nameof(entity.ProjectId), error.PropertyName);
		}

		[Fact(DisplayName =	"An empty GUID cannot be used as an optional aspect reference")]
		public void EmptyAspectIdentifier_IsRejected()
		{
			var entity = new EngineeringObject
			{
				ProjectId = Guid.NewGuid(),
				LocationId = Guid.NewGuid(),
				FunctionId = Guid.Empty
			};

			var result = _validation.Validate(entity);
			var error = Assert.Single(result.Errors);

			Assert.Equal(nameof(entity.FunctionId), error.PropertyName);
		}

		[Fact(DisplayName =	"A root aspect node does not require a parent or representing object")]
		public void RootNode_IsAccepted()
		{
			var node = new Location
			{
				ProjectId = Guid.NewGuid(),
				Designation = "AS1"
			};

			Assert.True(_validation.Validate(node).IsValid);
		}

		[Fact(DisplayName =	"An aspect node cannot be its own parent")]
		public void SelfParent_IsRejected()
		{
			var node = new Location
			{
				ProjectId = Guid.NewGuid(),
				Designation = "AS1"
			};

			node.ParentId = node.Id;

			var result = _validation.Validate(node);
			var error = Assert.Single(result.Errors);

			Assert.Equal(nameof(node.ParentId), error.PropertyName);
		}
	}
}