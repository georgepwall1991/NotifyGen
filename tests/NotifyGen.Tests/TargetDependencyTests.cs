using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace NotifyGen.Tests;

public class TargetDependencyTests
{
    [Fact]
    public void TargetSide_ChainAndDiamond_AreNormalizedIntoExplicitGraph()
    {
        const string source = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private string _firstName = string.Empty;

                [NotifyAlso(nameof(FirstName), NotifyFrom = true)]
                private string _fullName = string.Empty;

                [NotifyAlso(nameof(FullName), NotifyFrom = true)]
                private string _displayName = string.Empty;

                [NotifyAlso(nameof(FullName), NotifyFrom = true)]
                private string _searchText = string.Empty;

                [NotifyAlso(nameof(DisplayName), NotifyFrom = true)]
                [NotifyAlso(nameof(SearchText), NotifyFrom = true)]
                private string _summary = string.Empty;
            }
            """;

        var result = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
        using var assemblyStream = new MemoryStream();
        result.OutputCompilation.Emit(assemblyStream).Success.Should().BeTrue();
        var personType = Assembly.Load(assemblyStream.ToArray()).GetType("Person")!;
        var person = Activator.CreateInstance(personType)!;
        var changed = new List<string>();
        ((INotifyPropertyChanged)person).PropertyChanged += (_, args) =>
            changed.Add(args.PropertyName!);

        personType.GetProperty("FirstName")!.SetValue(person, "Ada");

        changed.Should().Equal("FirstName", "FullName", "DisplayName", "Summary", "SearchText");
    }

    [Fact]
    public void TargetSide_PartialPropertySource_RaisesDependentTarget()
    {
        const string source = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private string _name = string.Empty;

                [NotifyAlso(nameof(Name), NotifyFrom = true)]
                public partial string FullName { get; set; }
            }
            """;

        var result = GeneratorTestHelper.RunGeneratorWithLanguageVersionAndAssertCompiles(
            source,
            Microsoft.CodeAnalysis.CSharp.LanguageVersion.Preview
        );
        using var assemblyStream = new MemoryStream();
        result.OutputCompilation.Emit(assemblyStream).Success.Should().BeTrue();
        var personType = Assembly.Load(assemblyStream.ToArray()).GetType("Person")!;
        var person = Activator.CreateInstance(personType)!;
        var changed = new List<string>();
        ((INotifyPropertyChanged)person).PropertyChanged += (_, args) =>
            changed.Add(args.PropertyName!);

        personType.GetProperty("Name")!.SetValue(person, "Ada");

        changed.Should().Equal("Name", "FullName");
    }

    [Fact]
    public void TargetSide_OrdinaryComputedProperty_RaisesWhenGeneratedSourceChanges()
    {
        const string source = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private string _name = string.Empty;

                [NotifyAlso(nameof(Name), NotifyFrom = true)]
                public string DisplayName => Name;
            }
            """;

        var result = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
        using var assemblyStream = new MemoryStream();
        result.OutputCompilation.Emit(assemblyStream).Success.Should().BeTrue();
        var personType = Assembly.Load(assemblyStream.ToArray()).GetType("Person")!;
        var person = Activator.CreateInstance(personType)!;
        var changed = new List<string>();
        ((INotifyPropertyChanged)person).PropertyChanged += (_, args) =>
            changed.Add(args.PropertyName!);

        personType.GetProperty("Name")!.SetValue(person, "Ada");

        changed.Should().Equal("Name", "DisplayName");
    }

}
