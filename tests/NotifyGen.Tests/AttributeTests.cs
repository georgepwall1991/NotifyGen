using FluentAssertions;

namespace NotifyGen.Tests;

/// <summary>
/// Tests for attribute constructors and properties.
/// </summary>
public class AttributeTests
{
    #region NotifyAlsoAttribute Tests

    [Fact]
    public void NotifyAlsoAttribute_Constructor_WithValidName_SetsProperty()
    {
        // Arrange & Act
        var attribute = new NotifyAlsoAttribute("FullName");

        // Assert
        attribute.PropertyName.Should().Be("FullName");
    }

    [Fact]
    public void NotifyAlsoAttribute_Constructor_WithNull_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var action = () => new NotifyAlsoAttribute(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("propertyName");
    }

    [Fact]
    public void NotifyAlsoAttribute_Constructor_WithEmptyString_SetsProperty()
    {
        // Arrange & Act - empty strings are allowed (though semantically meaningless)
        var attribute = new NotifyAlsoAttribute("");

        // Assert
        attribute.PropertyName.Should().BeEmpty();
    }

    #endregion

    #region NotifyNameAttribute Tests

    [Fact]
    public void NotifyNameAttribute_Constructor_WithValidName_SetsProperty()
    {
        // Arrange & Act
        var attribute = new NotifyNameAttribute("IsVisible");

        // Assert
        attribute.Name.Should().Be("IsVisible");
    }

    [Fact]
    public void NotifyNameAttribute_Constructor_WithNull_ThrowsArgumentException()
    {
        // Arrange & Act
        var action = () => new NotifyNameAttribute(null!);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("name")
            .WithMessage("*cannot be null or empty*");
    }

    [Fact]
    public void NotifyNameAttribute_Constructor_WithEmpty_ThrowsArgumentException()
    {
        // Arrange & Act
        var action = () => new NotifyNameAttribute("");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("name")
            .WithMessage("*cannot be null or empty*");
    }

    [Fact]
    public void NotifyNameAttribute_Constructor_WithWhitespace_SetsProperty()
    {
        // Arrange & Act - whitespace-only strings pass IsNullOrEmpty check
        var attribute = new NotifyNameAttribute("   ");

        // Assert
        attribute.Name.Should().Be("   ");
    }

    #endregion

    #region NotifySetterAttribute Tests

    [Fact]
    public void NotifySetterAttribute_Constructor_SetsAccessLevel()
    {
        // Arrange & Act
        var attribute = new NotifySetterAttribute(AccessLevel.Private);

        // Assert
        attribute.Access.Should().Be(AccessLevel.Private);
    }

    [Theory]
    [InlineData(AccessLevel.Public)]
    [InlineData(AccessLevel.Protected)]
    [InlineData(AccessLevel.Internal)]
    [InlineData(AccessLevel.Private)]
    [InlineData(AccessLevel.ProtectedInternal)]
    [InlineData(AccessLevel.PrivateProtected)]
    public void NotifySetterAttribute_Constructor_WithAllAccessLevels_SetsCorrectly(AccessLevel level)
    {
        // Arrange & Act
        var attribute = new NotifySetterAttribute(level);

        // Assert
        attribute.Access.Should().Be(level);
    }

    #endregion

    #region NotifyAttribute Tests

    [Fact]
    public void NotifyAttribute_CanBeInstantiated()
    {
        // Arrange & Act
        var attribute = new NotifyAttribute();

        // Assert
        attribute.Should().NotBeNull();
    }

    [Fact]
    public void NotifyAttribute_HasCorrectUsage()
    {
        // Arrange & Act
        var usageAttribute = typeof(NotifyAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        usageAttribute.Should().NotBeNull();
        usageAttribute!.ValidOn.Should().Be(AttributeTargets.Class);
        usageAttribute.Inherited.Should().BeFalse();
        usageAttribute.AllowMultiple.Should().BeFalse();
    }

    #endregion

    #region NotifyIgnoreAttribute Tests

    [Fact]
    public void NotifyIgnoreAttribute_CanBeInstantiated()
    {
        // Arrange & Act
        var attribute = new NotifyIgnoreAttribute();

        // Assert
        attribute.Should().NotBeNull();
    }

    [Fact]
    public void NotifyIgnoreAttribute_HasCorrectUsage()
    {
        // Arrange & Act
        var usageAttribute = typeof(NotifyIgnoreAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        usageAttribute.Should().NotBeNull();
        usageAttribute!.ValidOn.Should().Be(AttributeTargets.Field);
        usageAttribute.Inherited.Should().BeFalse();
        usageAttribute.AllowMultiple.Should().BeFalse();
    }

    #endregion

    #region NotifyAlsoAttribute Usage Tests

    [Fact]
    public void NotifyAlsoAttribute_HasCorrectUsage()
    {
        // Arrange & Act
        var usageAttribute = typeof(NotifyAlsoAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        usageAttribute.Should().NotBeNull();
        usageAttribute!.ValidOn.Should().Be(AttributeTargets.Field);
        usageAttribute.Inherited.Should().BeFalse();
        usageAttribute.AllowMultiple.Should().BeTrue();
    }

    #endregion

    #region NotifyNameAttribute Usage Tests

    [Fact]
    public void NotifyNameAttribute_HasCorrectUsage()
    {
        // Arrange & Act
        var usageAttribute = typeof(NotifyNameAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        usageAttribute.Should().NotBeNull();
        usageAttribute!.ValidOn.Should().Be(AttributeTargets.Field);
        usageAttribute.Inherited.Should().BeFalse();
        usageAttribute.AllowMultiple.Should().BeFalse();
    }

    #endregion

    #region NotifySetterAttribute Usage Tests

    [Fact]
    public void NotifySetterAttribute_HasCorrectUsage()
    {
        // Arrange & Act
        var usageAttribute = typeof(NotifySetterAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        usageAttribute.Should().NotBeNull();
        usageAttribute!.ValidOn.Should().Be(AttributeTargets.Field);
        usageAttribute.Inherited.Should().BeFalse();
        usageAttribute.AllowMultiple.Should().BeFalse();
    }

    #endregion

    #region AccessLevel Enum Tests

    [Fact]
    public void AccessLevel_HasExpectedValues()
    {
        // Assert
        Enum.GetValues<AccessLevel>().Should().HaveCount(6);
        ((int)AccessLevel.Public).Should().Be(0);
        ((int)AccessLevel.Protected).Should().Be(1);
        ((int)AccessLevel.Internal).Should().Be(2);
        ((int)AccessLevel.Private).Should().Be(3);
        ((int)AccessLevel.ProtectedInternal).Should().Be(4);
        ((int)AccessLevel.PrivateProtected).Should().Be(5);
    }

    #endregion
}
