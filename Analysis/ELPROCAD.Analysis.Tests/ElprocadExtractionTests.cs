using ELPROCAD.Analysis.Configuration;
using ELPROCAD.Analysis.Extraction;
using Xunit;

namespace ELPROCAD.Analysis.Tests
{
    public sealed class ElprocadExtractionTests
    {
        private static string ProfileJson => File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Profiles", "ELPROCAD.json"));

        [Fact(DisplayName = "ELPROCAD creates one flat Location entry without a placement parent")]
        public void Location_IsFlat()
        {
            var extractor = ExtractionProfiles.LoadElprocadJson(ProfileJson);
            var result = extractor.Extract(new (string Tag, string? Value)[] { ("PLAC", "AS1"), ("BET1", "Q02") });
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
            var aspect = Assert.Single(result.Aspects);
            Assert.Equal(AspectKind.Location, aspect.Kind);
            Assert.Equal(AspectStructure.Flat, aspect.Structure);
            Assert.Equal("AS1.Q02", Assert.Single(aspect.Levels));
            Assert.Equal("ELPROCAD", extractor.ProfileId);
        }

        [Theory(DisplayName = "A placement is mandatory and cannot be inherited from a previous symbol")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" \t ")]
        public void EmptyPlacement_IsRejected(string? placement)
        {
            var extractor = ExtractionProfiles.LoadElprocadJson(ProfileJson);
            Assert.True(extractor.Extract(new (string Tag, string? Value)[] { ("PLAC", "AS1"), ("BET1", "Q01") }).IsValid);
            var result = extractor.Extract(new (string Tag, string? Value)[] { ("PLAC", placement), ("BET1", "Q02") });
            Assert.False(result.IsValid);
            Assert.Empty(result.Aspects);
            Assert.Equal("PLAC", Assert.Single(result.Errors).AttributeTag);
        }

        [Fact(DisplayName = "Missing placement and designation return separate errors without partial results")]
        public void MissingAttributes_AreRejected()
        {
            var result = ExtractionProfiles.LoadElprocadJson(ProfileJson).Extract(Array.Empty<(string, string?)>());
            Assert.False(result.IsValid);
            Assert.Empty(result.Aspects);
            Assert.Equal(new[] { "PLAC", "BET1" }, result.Errors.Select(x => x.AttributeTag));
        }

        [Theory(DisplayName = "An empty designation does not create a placement-only entry")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void EmptyDesignation_IsRejected(string? designation)
        {
            var result = ExtractionProfiles.LoadElprocadJson(ProfileJson).Extract(new (string Tag, string? Value)[] { ("PLAC", "AS1"), ("BET1", designation) });
            Assert.False(result.IsValid);
            Assert.Empty(result.Aspects);
            Assert.Equal("BET1", Assert.Single(result.Errors).AttributeTag);
        }

        [Fact(DisplayName = "Attribute tags are case insensitive while trimmed designation case is preserved")]
        public void AttributeNormalization_IsPredictable()
        {
            var result = ExtractionProfiles.LoadElprocadJson(ProfileJson).Extract(new (string Tag, string? Value)[] { ("plac", " As1 "), ("bet1", " q02 ") });
            Assert.Equal("As1.q02", Assert.Single(Assert.Single(result.Aspects).Levels));
        }

        [Fact(DisplayName = "BET2, XREF and boundary-like attributes do not create or override aspects")]
        public void UnusedAttributes_AreIgnored()
        {
            var result = ExtractionProfiles.LoadElprocadJson(ProfileJson).Extract(new (string Tag, string? Value)[] {
                ("PLAC", "AS1"), ("BET1", "Q02"), ("BET2", "Function"),
                ("XREF", "Other drawing"), ("BOX_LOCATION", "AS9") });
            Assert.Equal("AS1.Q02", Assert.Single(Assert.Single(result.Aspects).Levels));
        }

        [Theory(DisplayName = "Duplicate source tags are rejected even when their values agree")]
        [InlineData("PLAC", "AS1")]
        [InlineData("bet1", "Q02")]
        public void DuplicateSourceTag_IsRejected(string tag, string value)
        {
            var result = ExtractionProfiles.LoadElprocadJson(ProfileJson).Extract(new (string Tag, string? Value)[] {
                ("PLAC", "AS1"), ("BET1", "Q02"), (tag, value) });
            Assert.False(result.IsValid);
            Assert.Empty(result.Aspects);
            Assert.Single(result.Errors);
        }

        [Fact(DisplayName = "Configured attribute names and separator are used without hardcoded PLAC or BET1")]
        public void Profile_ControlsMapping()
        {
            var json = ProfileJson.Replace("\"PLAC\"", "\"LOCATION\"")
                .Replace("\"BET1\"", "\"DEVICE\"").Replace("\".\"", "\"/\"");
            var result = ExtractionProfiles.LoadElprocadJson(json).Extract(new (string Tag, string? Value)[] {
                ("LOCATION", "AS1"), ("DEVICE", "Q02"), ("PLAC", "Wrong"), ("BET1", "Wrong") });
            Assert.Equal("AS1/Q02", Assert.Single(Assert.Single(result.Aspects).Levels));
        }

        [Fact(DisplayName = "Different placements produce separate flat entries for the same designation")]
        public void Placements_AreIndependent()
        {
            var extractor = ExtractionProfiles.LoadElprocadJson(ProfileJson);
            var first = extractor.Extract(new (string Tag, string? Value)[] { ("PLAC", "AS1"), ("BET1", "Q02") });
            var second = extractor.Extract(new (string Tag, string? Value)[] { ("PLAC", "AS2"), ("BET1", "Q02") });
            Assert.Equal("AS1.Q02", first.Aspects[0].Levels[0]);
            Assert.Equal("AS2.Q02", second.Aspects[0].Levels[0]);
        }

        [Fact(DisplayName = "Invalid configuration cannot silently enable IEC81346 or boundary extraction")]
        public void UnsupportedStrategy_IsRejected()
        {
            Assert.Throws<FormatException>(() => ExtractionProfiles.LoadElprocadJson(ProfileJson.Replace("\"extractor\": \"ELPROCAD\"", "\"extractor\": \"IEC81346\"")));
            Assert.Throws<FormatException>(() => ExtractionProfiles.LoadElprocadJson(ProfileJson.Replace("\"Ignore\"", "\"Use\"")));
            Assert.Throws<FormatException>(() => ExtractionProfiles.LoadElprocadJson(ProfileJson.Replace("\"Flat\"", "\"Hierarchical\"")));
            Assert.Throws<FormatException>(() => ExtractionProfiles.LoadElprocadJson(ProfileJson.Replace("[\"Location\"]", "[\"Location\", \"Function\"]")));
        }

        [Fact(DisplayName = "Malformed, incomplete, unknown and duplicate configuration settings are rejected")]
        public void InvalidJson_IsRejected()
        {
            foreach (var json in new[] {
                "", "null", "{}", "{", ProfileJson + "{}",
                ProfileJson.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2"),
                ProfileJson.Replace("\"boundaries\": \"Ignore\",", ""),
                ProfileJson.Replace("\"separator\": \".\"", "\"seperator\": \".\""),
                ProfileJson.Replace("\"schemaVersion\": 1", "\"unexpected\": true, \"schemaVersion\": 1"),
                ProfileJson.Replace("\"separator\": \".\"", "\"separator\": \".\", \"extra\": true"),
                ProfileJson.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"schemaVersion\": 1") })
                Assert.True(Record.Exception(() => ExtractionProfiles.LoadElprocadJson(json)) is FormatException, json);
        }

        [Fact(DisplayName = "Source tags must be distinct and the separator must be unambiguous")]
        public void InvalidMapping_IsRejected()
        {
            Assert.Throws<FormatException>(() => ExtractionProfiles.LoadElprocadJson(ProfileJson.Replace("\"BET1\"", "\"plac\"")));
            Assert.Throws<FormatException>(() => ExtractionProfiles.LoadElprocadJson(ProfileJson.Replace("\"PLAC\"", "\" \"")));
            Assert.Throws<FormatException>(() => ExtractionProfiles.LoadElprocadJson(ProfileJson.Replace("\".\"", "\"\"")));
            Assert.Throws<FormatException>(() => ExtractionProfiles.LoadElprocadJson(ProfileJson.Replace("\".\"", "\"..\"")));
        }

        [Fact(DisplayName = "Punctuation inside source values does not create a recursive Location hierarchy")]
        public void Punctuation_DoesNotCreateHierarchy()
        {
            var extractor = ExtractionProfiles.LoadElprocadJson(ProfileJson);
            var result = extractor.Extract(new (string Tag, string? Value)[] { ("PLAC", "AS1"), ("BET1", "Q.02") });
            Assert.True(result.IsValid);
            Assert.Equal("AS1.Q.02", Assert.Single(Assert.Single(result.Aspects).Levels));
        }

        [Fact(DisplayName = "An extraction result remains independent of subsequent input changes")]
        public void Result_IsSnapshot()
        {
            var values = new (string Tag, string? Value)[] { ("PLAC", "AS1"), ("BET1", "Q02") };
            var result = ExtractionProfiles.LoadElprocadJson(ProfileJson).Extract(values);
            values[0] = ("PLAC", "AS2");
            Assert.Equal("AS1.Q02", result.Aspects[0].Levels[0]);
            Assert.Throws<NotSupportedException>(() => ((IList<string>)result.Aspects[0].Levels)[0] = "Changed");
        }

        [Fact(DisplayName = "Boundary-related data cannot supply a missing ELPROCAD placement")]
        public void BoundaryLikeAttributes_DoNotSupplyPlacement()
        {
            var result = ExtractionProfiles.LoadElprocadJson(ProfileJson).Extract(new (string, string?)[] {
                ("BOX_LOCATION", "AS1"), ("BET1", "Q02"), ("BET2", "AS2") });
            Assert.False(result.IsValid);
            Assert.Equal("PLAC", Assert.Single(result.Errors).AttributeTag);
            Assert.Empty(result.Aspects);
        }

        [Fact(DisplayName = "Configuration values retain their JSON types and escaped text is read correctly")]
        public void JsonTypes_AreValidated()
        {
            Assert.Throws<FormatException>(() => ExtractionProfiles.LoadElprocadJson(ProfileJson.Replace("\"schemaVersion\": 1", "\"schemaVersion\": \"1\"")));
            Assert.Throws<FormatException>(() => ExtractionProfiles.LoadElprocadJson(ProfileJson.Replace("\"placementAttribute\": \"PLAC\"", "\"placementAttribute\": 12")));
            Assert.Throws<FormatException>(() => ExtractionProfiles.LoadElprocadJson(ProfileJson.Replace("\"Location\"", "null")));
            var json = ProfileJson.Replace("\"profileId\": \"ELPROCAD\"", "\"profileId\": \"Test {A} \\\"quoted\\\"\"");
            Assert.Equal("Test {A} \"quoted\"", ExtractionProfiles.LoadElprocadJson(json).ProfileId);
        }
    }
}


