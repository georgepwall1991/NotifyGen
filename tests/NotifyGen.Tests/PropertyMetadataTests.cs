using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace NotifyGen.Tests;

public class PropertyMetadataTests
{
    [Fact]
    public void Generator_ForwardsPropertyTargetableFieldAttributes()
    {
        const string source = """
            using System;
            using System.ComponentModel.DataAnnotations;
            using NotifyGen;

            namespace MetadataFixture;

            [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
            public sealed class MarkerAttribute : Attribute
            {
                public MarkerAttribute(string value) => Value = value;
                public string Value { get; }
                public int Number { get; set; }
            }

            [AttributeUsage(AttributeTargets.Field)]
            public sealed class FieldOnlyAttribute : Attribute
            {
            }

            [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
            public class BaseMetadataAttribute : Attribute
            {
            }

            [AttributeUsage(AttributeTargets.Field, Inherited = false)]
            public class BaseFieldOnlyAttribute : Attribute
            {
            }

            public sealed class DerivedInheritedFieldOnlyAttribute : BaseFieldOnlyAttribute
            {
            }

            [AttributeUsage(AttributeTargets.Field)]
            public sealed class DerivedFieldOnlyAttribute : BaseMetadataAttribute
            {
            }

            [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
            public sealed class ObjectMarkerAttribute : Attribute
            {
                public ObjectMarkerAttribute(object value) => Value = value;
                public object Value { get; }
            }

            [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
            public sealed class KeywordMarkerAttribute : Attribute
            {
                public int @class { get; set; }
            }

            [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
            public sealed class ObjectArrayMarkerAttribute : Attribute
            {
                public ObjectArrayMarkerAttribute(object[] values) => Values = values;
                public object[] Values { get; }
            }

            [Notify]
            public partial class MetadataEntity
            {
                [Required(AllowEmptyStrings = true)]
                [StringLength(12, MinimumLength = 2)]
                [Marker(nameof(DisplayName), Number = 3)]
                [FieldOnly]
                [DerivedFieldOnly]
                [DerivedInheritedFieldOnly]
                [ObjectMarker((byte)7)]
                [ObjectArrayMarker(new object[] { (byte)7, (short)8 })]
                [KeywordMarker(@class = 3)]
                private string _displayName = string.Empty;
            }
            """;

        var result = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
        var generated = GeneratorTestHelper.GetGeneratedSource(
            result.RunResult,
            "MetadataEntity.g.cs"
        );

        generated.Should().NotBeNull();
        generated.Should().Contain(
            "[global::System.ComponentModel.DataAnnotations.RequiredAttribute(AllowEmptyStrings = true)]"
        );
        generated.Should().Contain(
            "[global::System.ComponentModel.DataAnnotations.StringLengthAttribute(12, MinimumLength = 2)]"
        );
        generated.Should().Contain(
            "[global::MetadataFixture.MarkerAttribute(\"DisplayName\", Number = 3)]"
        );
        generated.Should().Contain(
            "[global::MetadataFixture.ObjectMarkerAttribute((byte)7)]"
        );
        generated.Should().Contain(
            "[global::MetadataFixture.ObjectArrayMarkerAttribute(new global::System.Object[] { (byte)7, (short)8 })]"
        );
        generated.Should().Contain(
            "[global::MetadataFixture.KeywordMarkerAttribute(@class = 3)]"
        );
        generated.Should().NotContain("FieldOnlyAttribute");
        generated.Should().NotContain("DerivedFieldOnlyAttribute");
        generated.Should().NotContain("DerivedInheritedFieldOnlyAttribute");

        using var assemblyStream = new System.IO.MemoryStream();
        result.OutputCompilation.Emit(assemblyStream).Success.Should().BeTrue();
        var type = Assembly.Load(assemblyStream.ToArray()).GetType(
            "MetadataFixture.MetadataEntity"
        );
        var property = type!.GetProperty("DisplayName")
            ?? throw new InvalidOperationException("Generated property was not emitted.");

        property.GetCustomAttribute<RequiredAttribute>()!.AllowEmptyStrings.Should().BeTrue();
        property.GetCustomAttribute<StringLengthAttribute>()!.MinimumLength.Should().Be(2);
        var marker = property.GetCustomAttributes().Single(attribute =>
            attribute.GetType().Name == "MarkerAttribute"
        );
        marker.GetType().GetProperty("Value")!.GetValue(marker).Should().Be("DisplayName");
        marker.GetType().GetProperty("Number")!.GetValue(marker).Should().Be(3);
        var objectMarker = property.GetCustomAttributes().Single(attribute =>
            attribute.GetType().Name == "ObjectMarkerAttribute"
        );
        objectMarker.GetType().GetProperty("Value")!.GetValue(objectMarker).Should().Be((byte)7);
        var objectArrayMarker = property.GetCustomAttributes().Single(attribute =>
            attribute.GetType().Name == "ObjectArrayMarkerAttribute"
        );
        var objectArrayValues = (object[])objectArrayMarker
            .GetType()
            .GetProperty("Values")!
            .GetValue(objectArrayMarker)!;
        objectArrayValues.Should().Equal((byte)7, (short)8);
        var keywordMarker = property.GetCustomAttributes().Single(attribute =>
            attribute.GetType().Name == "KeywordMarkerAttribute"
        );
        keywordMarker.GetType().GetProperty("class")!.GetValue(keywordMarker).Should().Be(3);
        property.GetCustomAttributes().Should().NotContain(attribute =>
            attribute.GetType().Name == "FieldOnlyAttribute"
        );
    }

    [Fact]
    public void Generator_SkipsFileLocalPropertyAttributesWithoutBreakingCompilation()
    {
        const string source = """
            using System;
            using NotifyGen;

            [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
            file sealed class LocalMetadataAttribute : Attribute
            {
                public LocalMetadataAttribute(string value) { }
            }

            [Notify]
            public partial class LocalMetadataEntity
            {
                [LocalMetadata("name")]
                private string _name = string.Empty;
            }
            """;

        var result = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
        using var assemblyStream = new System.IO.MemoryStream();
        result.OutputCompilation.Emit(assemblyStream).Success.Should().BeTrue();
        var generated = GeneratorTestHelper.GetGeneratedSource(
            result.RunResult,
            "LocalMetadataEntity.g.cs"
        );

        generated.Should().NotBeNull();
        generated.Should().NotContain("LocalMetadataAttribute");
    }


    [Fact]
    public void Generator_SkipsAttributesWithFileLocalArgumentTypes()
    {
        const string source = """
            using System;
            using NotifyGen;

            file enum LocalKind { Value }

            [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
            public sealed class LocalKindAttribute : Attribute
            {
                public LocalKindAttribute(object value) { }
            }

            [Notify]
            public partial class LocalKindEntity
            {
                [LocalKind(LocalKind.Value)]
                private string _name = string.Empty;
            }
            """;

        var result = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
        using var assemblyStream = new System.IO.MemoryStream();
        result.OutputCompilation.Emit(assemblyStream).Success.Should().BeTrue();
        var generated = GeneratorTestHelper.GetGeneratedSource(
            result.RunResult,
            "LocalKindEntity.g.cs"
        );

        generated.Should().NotBeNull();
        generated.Should().NotContain("LocalKindAttribute");
    }

}
