using System.Collections.Immutable;
using FluentAssertions;
using NotifyGen.Generator;

namespace NotifyGen.Tests;

/// <summary>
/// Tests for ClassInfo and FieldInfo IEquatable implementations.
/// </summary>
public class EqualityTests
{
    #region ClassInfo Tests

    [Fact]
    public void ClassInfo_Equals_IdenticalValues_ReturnsTrue()
    {
        // Arrange
        var fields = ImmutableArray.Create(
            new FieldInfo("_name", "Name", "string", false, ImmutableArray<string>.Empty, null));

        var a = new ClassInfo("TestNamespace", "Person", "", "public", false, fields);
        var b = new ClassInfo("TestNamespace", "Person", "", "public", false, fields);

        // Act & Assert
        a.Equals(b).Should().BeTrue();
        b.Equals(a).Should().BeTrue();
    }

    [Fact]
    public void ClassInfo_Equals_DifferentNamespace_ReturnsFalse()
    {
        // Arrange
        var fields = ImmutableArray<FieldInfo>.Empty;
        var a = new ClassInfo("Namespace1", "Person", "", "public", false, fields);
        var b = new ClassInfo("Namespace2", "Person", "", "public", false, fields);

        // Act & Assert
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void ClassInfo_Equals_DifferentClassName_ReturnsFalse()
    {
        // Arrange
        var fields = ImmutableArray<FieldInfo>.Empty;
        var a = new ClassInfo("TestNamespace", "Person", "", "public", false, fields);
        var b = new ClassInfo("TestNamespace", "Employee", "", "public", false, fields);

        // Act & Assert
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void ClassInfo_Equals_DifferentTypeParameters_ReturnsFalse()
    {
        // Arrange
        var fields = ImmutableArray<FieldInfo>.Empty;
        var a = new ClassInfo("TestNamespace", "Wrapper", "<T>", "public", false, fields);
        var b = new ClassInfo("TestNamespace", "Wrapper", "<T, U>", "public", false, fields);

        // Act & Assert
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void ClassInfo_Equals_DifferentAccessibility_ReturnsFalse()
    {
        // Arrange
        var fields = ImmutableArray<FieldInfo>.Empty;
        var a = new ClassInfo("TestNamespace", "Person", "", "public", false, fields);
        var b = new ClassInfo("TestNamespace", "Person", "", "internal", false, fields);

        // Act & Assert
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void ClassInfo_Equals_DifferentAlreadyImplementsInpc_ReturnsFalse()
    {
        // Arrange
        var fields = ImmutableArray<FieldInfo>.Empty;
        var a = new ClassInfo("TestNamespace", "Person", "", "public", false, fields);
        var b = new ClassInfo("TestNamespace", "Person", "", "public", true, fields);

        // Act & Assert
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void ClassInfo_Equals_DifferentFields_ReturnsFalse()
    {
        // Arrange
        var fieldsA = ImmutableArray.Create(
            new FieldInfo("_name", "Name", "string", false, ImmutableArray<string>.Empty, null));
        var fieldsB = ImmutableArray.Create(
            new FieldInfo("_age", "Age", "int", false, ImmutableArray<string>.Empty, null));

        var a = new ClassInfo("TestNamespace", "Person", "", "public", false, fieldsA);
        var b = new ClassInfo("TestNamespace", "Person", "", "public", false, fieldsB);

        // Act & Assert
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void ClassInfo_Equals_EmptyFields_BothEmpty_ReturnsTrue()
    {
        // Arrange
        var a = new ClassInfo("TestNamespace", "Person", "", "public", false, ImmutableArray<FieldInfo>.Empty);
        var b = new ClassInfo("TestNamespace", "Person", "", "public", false, ImmutableArray<FieldInfo>.Empty);

        // Act & Assert
        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void ClassInfo_Equals_Object_WithNull_ReturnsFalse()
    {
        // Arrange
        var classInfo = new ClassInfo("TestNamespace", "Person", "", "public", false, ImmutableArray<FieldInfo>.Empty);

        // Act & Assert
        classInfo.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void ClassInfo_Equals_Object_WithDifferentType_ReturnsFalse()
    {
        // Arrange
        var classInfo = new ClassInfo("TestNamespace", "Person", "", "public", false, ImmutableArray<FieldInfo>.Empty);

        // Act & Assert
        classInfo.Equals("not a ClassInfo").Should().BeFalse();
    }

    [Fact]
    public void ClassInfo_Equals_Object_WithSameClassInfo_ReturnsTrue()
    {
        // Arrange
        var a = new ClassInfo("TestNamespace", "Person", "", "public", false, ImmutableArray<FieldInfo>.Empty);
        object b = new ClassInfo("TestNamespace", "Person", "", "public", false, ImmutableArray<FieldInfo>.Empty);

        // Act & Assert
        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void ClassInfo_GetHashCode_SameValues_ReturnsSameHash()
    {
        // Arrange
        var fields = ImmutableArray.Create(
            new FieldInfo("_name", "Name", "string", false, ImmutableArray<string>.Empty, null));

        var a = new ClassInfo("TestNamespace", "Person", "", "public", false, fields);
        var b = new ClassInfo("TestNamespace", "Person", "", "public", false, fields);

        // Act & Assert
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void ClassInfo_GetHashCode_EmptyFields_Succeeds()
    {
        // Arrange
        var classInfo = new ClassInfo("TestNamespace", "Person", "", "public", false, ImmutableArray<FieldInfo>.Empty);

        // Act
        var hash = classInfo.GetHashCode();

        // Assert
        hash.Should().NotBe(0); // Should produce a valid hash
    }

    [Fact]
    public void ClassInfo_GetHashCode_WithFields_IncludesFirstFieldHash()
    {
        // Arrange
        var fields1 = ImmutableArray.Create(
            new FieldInfo("_name", "Name", "string", false, ImmutableArray<string>.Empty, null));
        var fields2 = ImmutableArray.Create(
            new FieldInfo("_age", "Age", "int", false, ImmutableArray<string>.Empty, null));

        var a = new ClassInfo("TestNamespace", "Person", "", "public", false, fields1);
        var b = new ClassInfo("TestNamespace", "Person", "", "public", false, fields2);

        // Act & Assert - Different first fields should typically produce different hashes
        a.GetHashCode().Should().NotBe(b.GetHashCode());
    }

    [Fact]
    public void ClassInfo_OperatorEquals_Works()
    {
        // Arrange
        var a = new ClassInfo("TestNamespace", "Person", "", "public", false, ImmutableArray<FieldInfo>.Empty);
        var b = new ClassInfo("TestNamespace", "Person", "", "public", false, ImmutableArray<FieldInfo>.Empty);
        var c = new ClassInfo("TestNamespace", "Employee", "", "public", false, ImmutableArray<FieldInfo>.Empty);

        // Act & Assert
        (a == b).Should().BeTrue();
        (a == c).Should().BeFalse();
    }

    [Fact]
    public void ClassInfo_OperatorNotEquals_Works()
    {
        // Arrange
        var a = new ClassInfo("TestNamespace", "Person", "", "public", false, ImmutableArray<FieldInfo>.Empty);
        var b = new ClassInfo("TestNamespace", "Person", "", "public", false, ImmutableArray<FieldInfo>.Empty);
        var c = new ClassInfo("TestNamespace", "Employee", "", "public", false, ImmutableArray<FieldInfo>.Empty);

        // Act & Assert
        (a != b).Should().BeFalse();
        (a != c).Should().BeTrue();
    }

    #endregion

    #region FieldInfo Tests

    [Fact]
    public void FieldInfo_Equals_IdenticalValues_ReturnsTrue()
    {
        // Arrange
        var alsoNotify = ImmutableArray.Create("FullName");
        var a = new FieldInfo("_name", "Name", "string", false, alsoNotify, "private");
        var b = new FieldInfo("_name", "Name", "string", false, alsoNotify, "private");

        // Act & Assert
        a.Equals(b).Should().BeTrue();
        b.Equals(a).Should().BeTrue();
    }

    [Fact]
    public void FieldInfo_Equals_DifferentFieldName_ReturnsFalse()
    {
        // Arrange
        var a = new FieldInfo("_firstName", "FirstName", "string", false, ImmutableArray<string>.Empty, null);
        var b = new FieldInfo("_lastName", "LastName", "string", false, ImmutableArray<string>.Empty, null);

        // Act & Assert
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void FieldInfo_Equals_DifferentPropertyName_ReturnsFalse()
    {
        // Arrange
        var a = new FieldInfo("_name", "Name", "string", false, ImmutableArray<string>.Empty, null);
        var b = new FieldInfo("_name", "FullName", "string", false, ImmutableArray<string>.Empty, null);

        // Act & Assert
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void FieldInfo_Equals_DifferentTypeName_ReturnsFalse()
    {
        // Arrange
        var a = new FieldInfo("_value", "Value", "string", false, ImmutableArray<string>.Empty, null);
        var b = new FieldInfo("_value", "Value", "int", false, ImmutableArray<string>.Empty, null);

        // Act & Assert
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void FieldInfo_Equals_DifferentIsNullable_ReturnsFalse()
    {
        // Arrange
        var a = new FieldInfo("_name", "Name", "string", false, ImmutableArray<string>.Empty, null);
        var b = new FieldInfo("_name", "Name", "string", true, ImmutableArray<string>.Empty, null);

        // Act & Assert
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void FieldInfo_Equals_DifferentSetterAccess_ReturnsFalse()
    {
        // Arrange
        var a = new FieldInfo("_name", "Name", "string", false, ImmutableArray<string>.Empty, "private");
        var b = new FieldInfo("_name", "Name", "string", false, ImmutableArray<string>.Empty, "protected");

        // Act & Assert
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void FieldInfo_Equals_DifferentAlsoNotify_ReturnsFalse()
    {
        // Arrange
        var alsoNotifyA = ImmutableArray.Create("FullName");
        var alsoNotifyB = ImmutableArray.Create("DisplayName");
        var a = new FieldInfo("_name", "Name", "string", false, alsoNotifyA, null);
        var b = new FieldInfo("_name", "Name", "string", false, alsoNotifyB, null);

        // Act & Assert
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void FieldInfo_Equals_DifferentAlsoNotifyLength_ReturnsFalse()
    {
        // Arrange
        var alsoNotifyA = ImmutableArray.Create("FullName");
        var alsoNotifyB = ImmutableArray.Create("FullName", "DisplayName");
        var a = new FieldInfo("_name", "Name", "string", false, alsoNotifyA, null);
        var b = new FieldInfo("_name", "Name", "string", false, alsoNotifyB, null);

        // Act & Assert
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void FieldInfo_Equals_EmptyAlsoNotify_BothEmpty_ReturnsTrue()
    {
        // Arrange
        var a = new FieldInfo("_name", "Name", "string", false, ImmutableArray<string>.Empty, null);
        var b = new FieldInfo("_name", "Name", "string", false, ImmutableArray<string>.Empty, null);

        // Act & Assert
        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void FieldInfo_Equals_Object_WithNull_ReturnsFalse()
    {
        // Arrange
        var fieldInfo = new FieldInfo("_name", "Name", "string", false, ImmutableArray<string>.Empty, null);

        // Act & Assert
        fieldInfo.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void FieldInfo_Equals_Object_WithDifferentType_ReturnsFalse()
    {
        // Arrange
        var fieldInfo = new FieldInfo("_name", "Name", "string", false, ImmutableArray<string>.Empty, null);

        // Act & Assert
        fieldInfo.Equals("not a FieldInfo").Should().BeFalse();
    }

    [Fact]
    public void FieldInfo_Equals_Object_WithSameFieldInfo_ReturnsTrue()
    {
        // Arrange
        var a = new FieldInfo("_name", "Name", "string", false, ImmutableArray<string>.Empty, null);
        object b = new FieldInfo("_name", "Name", "string", false, ImmutableArray<string>.Empty, null);

        // Act & Assert
        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void FieldInfo_GetHashCode_SameValues_ReturnsSameHash()
    {
        // Arrange
        var alsoNotify = ImmutableArray.Create("FullName");
        var a = new FieldInfo("_name", "Name", "string", false, alsoNotify, "private");
        var b = new FieldInfo("_name", "Name", "string", false, alsoNotify, "private");

        // Act & Assert
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void FieldInfo_GetHashCode_WithNullSetterAccess_Succeeds()
    {
        // Arrange
        var fieldInfo = new FieldInfo("_name", "Name", "string", false, ImmutableArray<string>.Empty, null);

        // Act
        var hash = fieldInfo.GetHashCode();

        // Assert
        hash.Should().NotBe(0);
    }

    [Fact]
    public void FieldInfo_GetHashCode_EmptyAlsoNotify_Succeeds()
    {
        // Arrange
        var fieldInfo = new FieldInfo("_name", "Name", "string", false, ImmutableArray<string>.Empty, null);

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
        var a = new FieldInfo("_name", "Name", "string", false, alsoNotify1, null);
        var b = new FieldInfo("_name", "Name", "string", false, alsoNotify2, null);

        // Act & Assert - Different first elements should typically produce different hashes
        a.GetHashCode().Should().NotBe(b.GetHashCode());
    }

    [Fact]
    public void FieldInfo_OperatorEquals_Works()
    {
        // Arrange
        var a = new FieldInfo("_name", "Name", "string", false, ImmutableArray<string>.Empty, null);
        var b = new FieldInfo("_name", "Name", "string", false, ImmutableArray<string>.Empty, null);
        var c = new FieldInfo("_age", "Age", "int", false, ImmutableArray<string>.Empty, null);

        // Act & Assert
        (a == b).Should().BeTrue();
        (a == c).Should().BeFalse();
    }

    [Fact]
    public void FieldInfo_OperatorNotEquals_Works()
    {
        // Arrange
        var a = new FieldInfo("_name", "Name", "string", false, ImmutableArray<string>.Empty, null);
        var b = new FieldInfo("_name", "Name", "string", false, ImmutableArray<string>.Empty, null);
        var c = new FieldInfo("_age", "Age", "int", false, ImmutableArray<string>.Empty, null);

        // Act & Assert
        (a != b).Should().BeFalse();
        (a != c).Should().BeTrue();
    }

    #endregion
}
