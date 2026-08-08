using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Xunit;

namespace NotifyGen.Tests;

public class SubPropertyNotificationTests
{
    [Fact]
    public void Generator_NotifyAlsoSubPropertyChanged_TracksChildReplacementAndChanges()
    {
        const string source = """
            using System;
            using System.ComponentModel;
            using NotifyGen;

            namespace SubPropertyFixture;

            public sealed class Address : INotifyPropertyChanged
            {
                public Address(string city) => City = city;

                public string City { get; private set; }

                public event PropertyChangedEventHandler? PropertyChanged;

                public void SetCity(string city)
                {
                    if (City == city)
                    {
                        return;
                    }

                    City = city;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(City)));
                }
            }

            [Notify]
            public partial class Person
            {
                [NotifyAlso(nameof(DisplayName), NotifyOnSubPropertyChanged = true)]
                private Address? _address = new Address("one");

                public string DisplayName => Address?.City ?? string.Empty;

                public Address GetInitialAddressDirectly() => _address!;

                private void __notifyGenSubProperty_AddressChanged(
                    object? sender,
                    PropertyChangedEventArgs args
                ) { }

                public void Replace(Address? address) => Address = address;
            }
            """;

        var result = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
        var generated = GeneratorTestHelper.GetGeneratedSource(result.RunResult, "Person.g.cs");
        generated.Should().NotBeNull();
        generated.Should().Contain("PropertyChanged += __notifyGenSubProperty_Address_");
        generated.Should().Contain("PropertyChanged -= __notifyGenSubProperty_Address_");

        using var assemblyStream = new MemoryStream();
        var emit = result.OutputCompilation.Emit(assemblyStream);
        emit.Success.Should().BeTrue(string.Join(Environment.NewLine, emit.Diagnostics));

        var assembly = Assembly.Load(assemblyStream.ToArray());
        var personType = assembly.GetType("SubPropertyFixture.Person")!;
        var addressType = assembly.GetType("SubPropertyFixture.Address")!;
        var person = Activator.CreateInstance(personType)!;
        var initialAddress = personType.GetMethod("GetInitialAddressDirectly")!.Invoke(person, null)!;

        var changed = new List<string>();
        ((INotifyPropertyChanged)person).PropertyChanged += (_, args) =>
            changed.Add(args.PropertyName!);

        addressType.GetMethod("SetCity")!.Invoke(initialAddress, new object[] { "before access" });
        changed.Should().BeEmpty("subscription starts when the generated property is first accessed");

        personType.GetProperty("Address")!.GetValue(person).Should().Be(initialAddress);
        personType.GetProperty("DisplayName")!.GetValue(person).Should().Be("before access");
        addressType.GetMethod("SetCity")!.Invoke(initialAddress, new object[] { "two" });
        changed.Should().Equal("DisplayName");

        var replacement = Activator.CreateInstance(addressType, "three")!;
        personType.GetMethod("Replace")!.Invoke(person, new object?[] { replacement });
        changed.Clear();

        addressType.GetMethod("SetCity")!.Invoke(initialAddress, new object[] { "stale" });
        changed.Should().BeEmpty("the old child must be unsubscribed");

        addressType.GetMethod("SetCity")!.Invoke(replacement, new object[] { "four" });
        changed.Should().Equal("DisplayName");

        personType.GetMethod("Replace")!.Invoke(person, new object?[] { null });
        changed.Clear();
        addressType.GetMethod("SetCity")!.Invoke(replacement, new object[] { "stale again" });
        changed.Should().BeEmpty("null replacement must unsubscribe the child");
    }

    [Fact]
    public void PartialProperty_NotifyAlsoSubPropertyChanged_GeneratesSubscription()
    {
        const string source = """
            using System.ComponentModel;
            using NotifyGen;

            public sealed class Address : INotifyPropertyChanged
            {
                public event PropertyChangedEventHandler? PropertyChanged;
            }

            [Notify]
            public partial class Person
            {
                [NotifyAlso(nameof(DisplayName), NotifyOnSubPropertyChanged = true)]
                public partial Address? Address { get; set; }

                public string DisplayName => Address is null ? string.Empty : "set";
            }
            """;

        var result = GeneratorTestHelper.RunGeneratorWithLanguageVersionAndAssertCompiles(
            source,
            Microsoft.CodeAnalysis.CSharp.LanguageVersion.Preview
        );
        var generated = GeneratorTestHelper.GetGeneratedSource(result.RunResult, "Person.g.cs");
        generated.Should().NotBeNull();

        generated.Should().Contain("public partial Address? Address");
        generated.Should().Contain("__notifyGenSubProperty_Address_");
        generated.Should().Contain("Ensure(field)");
        generated.Should().Contain("Update(value)");
    }
}
