using System.Collections.Immutable;
using FluentAssertions;
using NotifyGen.Generator;

namespace NotifyGen.Tests;

/// <summary>
/// Tests for generator value-model IEquatable implementations.
/// </summary>
public class EqualityTests
{
    #region Declaration Model Tests

    [Fact]
    public void TypeDeclarationInfo_Equals_IdenticalValues_ReturnsTrue()
    {
        var a = CreateDeclaration();
        var b = CreateDeclaration();

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void TypeDeclarationInfo_Equals_DifferentEmittedShape_ReturnsFalse()
    {
        var baseline = CreateDeclaration();
        var differentModifier = CreateDeclaration(
            requiredModifiers: ImmutableArray.Create("static")
        );
        var differentConstraint = CreateDeclaration(
            constraintClauses: ImmutableArray.Create("where T : struct")
        );
        var differentPartialState = CreateDeclaration(isPartial: false);

        baseline.Should().NotBe(differentModifier);
        baseline.Should().NotBe(differentConstraint);
        baseline.Should().NotBe(differentPartialState);
    }

    [Fact]
    public void NotificationTypeInfo_Equals_IncludesCompleteDeclarationChain()
    {
        var outer = CreateDeclaration(name: "Outer", metadataIdentity: "Tests.Outer");
        var target = CreateDeclaration(name: "Inner", metadataIdentity: "Tests.Outer+Inner");
        var differentTarget = CreateDeclaration(
            name: "Other",
            metadataIdentity: "Tests.Outer+Other"
        );
        var fields = ImmutableArray.Create(
            new FieldInfo(
                "_name",
                "Name",
                "string",
                false,
                ImmutableArray<string>.Empty,
                ImmutableArray<string>.Empty
            )
        );
        var a = new NotificationTypeInfo(
            "Tests",
            ImmutableArray.Create(outer, target),
            false,
            false,
            false,
            false,
            ImmutableArray<string>.Empty,
            fields
        );
        var b = new NotificationTypeInfo(
            "Tests",
            ImmutableArray.Create(outer, target),
            false,
            false,
            false,
            false,
            ImmutableArray<string>.Empty,
            fields
        );
        var different = new NotificationTypeInfo(
            "Tests",
            ImmutableArray.Create(outer, differentTarget),
            false,
            false,
            false,
            false,
            ImmutableArray<string>.Empty,
            fields
        );

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
        a.Should().NotBe(different);
    }

    private static TypeDeclarationInfo CreateDeclaration(
        string name = "Model",
        ImmutableArray<string> requiredModifiers = default,
        ImmutableArray<string> constraintClauses = default,
        string metadataIdentity = "Tests.Model",
        bool isPartial = true
    ) =>
        new(
            TypeDeclarationKind.Class,
            name,
            name,
            "public",
            requiredModifiers.IsDefault ? ImmutableArray<string>.Empty : requiredModifiers,
            ImmutableArray.Create("T"),
            constraintClauses.IsDefault ? ImmutableArray<string>.Empty : constraintClauses,
            metadataIdentity,
            isPartial
        );

    #endregion

    #region FieldInfo Tests

    [Fact]
    public void FieldInfo_Equals_IdenticalValues_ReturnsTrue()
    {
        // Arrange
        var alsoNotify = ImmutableArray.Create("FullName");
        var a = new FieldInfo(
            "_name",
            "Name",
            "string",
            false,
            alsoNotify,
            ImmutableArray<string>.Empty,
            "private"
        );
        var b = new FieldInfo(
            "_name",
            "Name",
            "string",
            false,
            alsoNotify,
            ImmutableArray<string>.Empty,
            "private"
        );

        // Act & Assert
        a.Equals(b).Should().BeTrue();
        b.Equals(a).Should().BeTrue();
    }

    [Fact]
    public void FieldInfo_Equals_DifferentFieldName_ReturnsFalse()
    {
        // Arrange
        var a = new FieldInfo(
            "_firstName",
            "FirstName",
            "string",
            false,
            ImmutableArray<string>.Empty,
            ImmutableArray<string>.Empty,
            null
        );
        var b = new FieldInfo(
            "_lastName",
            "LastName",
            "string",
            false,
            ImmutableArray<string>.Empty,
            ImmutableArray<string>.Empty,
            null
        );

        // Act & Assert
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void FieldInfo_Equals_DifferentPropertyName_ReturnsFalse()
    {
        // Arrange
        var a = new FieldInfo(
            "_name",
            "Name",
            "string",
            false,
            ImmutableArray<string>.Empty,
            ImmutableArray<string>.Empty,
            null
        );
        var b = new FieldInfo(
            "_name",
            "FullName",
            "string",
            false,
            ImmutableArray<string>.Empty,
            ImmutableArray<string>.Empty,
            null
        );

        // Act & Assert
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void FieldInfo_Equals_DifferentTypeName_ReturnsFalse()
    {
        // Arrange
        var a = new FieldInfo(
            "_value",
            "Value",
            "string",
            false,
            ImmutableArray<string>.Empty,
            ImmutableArray<string>.Empty,
            null
        );
        var b = new FieldInfo(
            "_value",
            "Value",
            "int",
            false,
            ImmutableArray<string>.Empty,
            ImmutableArray<string>.Empty,
            null
        );

        // Act & Assert
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void FieldInfo_Equals_DifferentIsNullable_ReturnsFalse()
    {
        // Arrange
        var a = new FieldInfo(
            "_name",
            "Name",
            "string",
            false,
            ImmutableArray<string>.Empty,
            ImmutableArray<string>.Empty,
            null
        );
        var b = new FieldInfo(
            "_name",
            "Name",
            "string",
            true,
            ImmutableArray<string>.Empty,
            ImmutableArray<string>.Empty,
            null
        );

        // Act & Assert
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void FieldInfo_Equals_DifferentSetterAccess_ReturnsFalse()
    {
        // Arrange
        var a = new FieldInfo(
            "_name",
            "Name",
            "string",
            false,
            ImmutableArray<string>.Empty,
            ImmutableArray<string>.Empty,
            "private"
        );
        var b = new FieldInfo(
            "_name",
            "Name",
            "string",
            false,
            ImmutableArray<string>.Empty,
            ImmutableArray<string>.Empty,
            "protected"
        );

        // Act & Assert
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void FieldInfo_Equals_DifferentAlsoNotify_ReturnsFalse()
    {
        // Arrange
        var alsoNotifyA = ImmutableArray.Create("FullName");
        var alsoNotifyB = ImmutableArray.Create("DisplayName");
        var a = new FieldInfo(
            "_name",
            "Name",
            "string",
            false,
            alsoNotifyA,
            ImmutableArray<string>.Empty,
            null
        );
        var b = new FieldInfo(
            "_name",
            "Name",
            "string",
            false,
            alsoNotifyB,
            ImmutableArray<string>.Empty,
            null
        );

        // Act & Assert
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void FieldInfo_Equals_DifferentAlsoNotifyLength_ReturnsFalse()
    {
        // Arrange
        var alsoNotifyA = ImmutableArray.Create("FullName");
        var alsoNotifyB = ImmutableArray.Create("FullName", "DisplayName");
        var a = new FieldInfo(
            "_name",
            "Name",
            "string",
            false,
            alsoNotifyA,
            ImmutableArray<string>.Empty,
            null
        );
        var b = new FieldInfo(
            "_name",
            "Name",
            "string",
            false,
            alsoNotifyB,
            ImmutableArray<string>.Empty,
            null
        );

        // Act & Assert
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void FieldInfo_Equals_EmptyAlsoNotify_BothEmpty_ReturnsTrue()
    {
        // Arrange
        var a = new FieldInfo(
            "_name",
            "Name",
            "string",
            false,
            ImmutableArray<string>.Empty,
            ImmutableArray<string>.Empty,
            null
        );
        var b = new FieldInfo(
            "_name",
            "Name",
            "string",
            false,
            ImmutableArray<string>.Empty,
            ImmutableArray<string>.Empty,
            null
        );

        // Act & Assert
        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void FieldInfo_Equals_Object_WithNull_ReturnsFalse()
    {
        // Arrange
        var fieldInfo = new FieldInfo(
            "_name",
            "Name",
            "string",
            false,
            ImmutableArray<string>.Empty,
            ImmutableArray<string>.Empty,
            null
        );

        // Act & Assert
        fieldInfo.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void FieldInfo_Equals_Object_WithDifferentType_ReturnsFalse()
    {
        // Arrange
        var fieldInfo = new FieldInfo(
            "_name",
            "Name",
            "string",
            false,
            ImmutableArray<string>.Empty,
            ImmutableArray<string>.Empty,
            null
        );

        // Act & Assert
        fieldInfo.Equals("not a FieldInfo").Should().BeFalse();
    }

    [Fact]
    public void FieldInfo_Equals_Object_WithSameFieldInfo_ReturnsTrue()
    {
        // Arrange
        var a = new FieldInfo(
            "_name",
            "Name",
            "string",
            false,
            ImmutableArray<string>.Empty,
            ImmutableArray<string>.Empty,
            null
        );
        object b = new FieldInfo(
            "_name",
            "Name",
            "string",
            false,
            ImmutableArray<string>.Empty,
            ImmutableArray<string>.Empty,
            null
        );

        // Act & Assert
        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void FieldInfo_GetHashCode_SameValues_ReturnsSameHash()
    {
        // Arrange
        var alsoNotify = ImmutableArray.Create("FullName");
        var a = new FieldInfo(
            "_name",
            "Name",
            "string",
            false,
            alsoNotify,
            ImmutableArray<string>.Empty,
            "private"
        );
        var b = new FieldInfo(
            "_name",
            "Name",
            "string",
            false,
            alsoNotify,
            ImmutableArray<string>.Empty,
            "private"
        );

        // Act & Assert
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void FieldInfo_GetHashCode_WithNullSetterAccess_Succeeds()
    {
        // Arrange
        var fieldInfo = new FieldInfo(
            "_name",
            "Name",
            "string",
            false,
            ImmutableArray<string>.Empty,
            ImmutableArray<string>.Empty,
            null
        );

        // Act
        var hash = fieldInfo.GetHashCode();

        // Assert
        hash.Should().NotBe(0);
    }

    [Fact]
    public void FieldInfo_GetHashCode_EmptyAlsoNotify_Succeeds()
    {
        // Arrange
        var fieldInfo = new FieldInfo(
            "_name",
            "Name",
            "string",
            false,
            ImmutableArray<string>.Empty,
            ImmutableArray<string>.Empty,
            null
        );

        // Act
        var hash = fieldInfo.GetHashCode();

        // Assert
        hash.Should().NotBe(0);
    }

    [Fact]
    public void FieldInfo_GetHashCode_WithAlsoNotify_IncludesFirstElementHash()
    {
        // Arrange
        var alsoNotify1 = ImmutableArray.Create("FullName");
        var alsoNotify2 = ImmutableArray.Create("DisplayName");
        var a = new FieldInfo(
            "_name",
            "Name",
            "string",
            false,
            alsoNotify1,
            ImmutableArray<string>.Empty,
            null
        );
        var b = new FieldInfo(
            "_name",
            "Name",
            "string",
            false,
            alsoNotify2,
            ImmutableArray<string>.Empty,
            null
        );

        // Act & Assert - Different first elements should typically produce different hashes
        a.GetHashCode().Should().NotBe(b.GetHashCode());
    }

    [Fact]
    public void FieldInfo_OperatorEquals_Works()
    {
        // Arrange
        var a = new FieldInfo(
            "_name",
            "Name",
            "string",
            false,
            ImmutableArray<string>.Empty,
            ImmutableArray<string>.Empty,
            null
        );
        var b = new FieldInfo(
            "_name",
            "Name",
            "string",
            false,
            ImmutableArray<string>.Empty,
            ImmutableArray<string>.Empty,
            null
        );
        var c = new FieldInfo(
            "_age",
            "Age",
            "int",
            false,
            ImmutableArray<string>.Empty,
            ImmutableArray<string>.Empty,
            null
        );

        // Act & Assert
        (a == b)
            .Should()
            .BeTrue();
        (a == c).Should().BeFalse();
    }

    [Fact]
    public void FieldInfo_OperatorNotEquals_Works()
    {
        // Arrange
        var a = new FieldInfo(
            "_name",
            "Name",
            "string",
            false,
            ImmutableArray<string>.Empty,
            ImmutableArray<string>.Empty,
            null
        );
        var b = new FieldInfo(
            "_name",
            "Name",
            "string",
            false,
            ImmutableArray<string>.Empty,
            ImmutableArray<string>.Empty,
            null
        );
        var c = new FieldInfo(
            "_age",
            "Age",
            "int",
            false,
            ImmutableArray<string>.Empty,
            ImmutableArray<string>.Empty,
            null
        );

        // Act & Assert
        (a != b)
            .Should()
            .BeFalse();
        (a != c).Should().BeTrue();
    }

    #endregion
}
