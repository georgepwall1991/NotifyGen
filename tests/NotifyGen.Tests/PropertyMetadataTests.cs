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

            [Notify]
            public partial class MetadataEntity
            {
                [Required(AllowEmptyStrings = true)]
                [StringLength(12, MinimumLength = 2)]
                [Marker(nameof(DisplayName), Number = 3)]
                [FieldOnly]
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
        generated.Should().NotContain("FieldOnlyAttribute");

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
        property.GetCustomAttributes().Should().NotContain(attribute =>
            attribute.GetType().Name == "FieldOnlyAttribute"
        );
    }
}
