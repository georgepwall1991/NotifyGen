using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace NotifyGen.Tests;

public class Cycle4Tests
{
    [Fact]
    public void Generator_FieldNameStartingWithDigit_DoesNotEmitInvalidProperty()
    {
        const string source = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private string _1name = string.Empty;
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .Should().BeEmpty();
        result.RunResult.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_NotifyNameInvalidIdentifier_DoesNotEmitInvalidProperty()
    {
        const string source = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                [NotifyName(" ")]
                private string _name = string.Empty;
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .Should().BeEmpty();
        result.RunResult.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_NotifyNameKeyword_DoesNotEmitInvalidProperty()
    {
        const string source = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                [NotifyName("class")]
                private string _name = string.Empty;
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .Should().BeEmpty();
        result.RunResult.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_FileLocalFieldType_IsSkippedWithoutLeakingType()
    {
        const string source = """
            using NotifyGen;

            file sealed class LocalValue { }

            [Notify]
            public partial class Person
            {
                private LocalValue _value = new();
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        result.OutputCompilation.GetDiagnostics()
            .Should().Contain(diagnostic => diagnostic.Id == "CS9051");
        result.RunResult.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_HostsExistingInpcWithEventArgsInvoker()
    {
        const string source = """
            using System.ComponentModel;
            using NotifyGen;

            public abstract class CollectionBase : INotifyPropertyChanged
            {
                public event PropertyChangedEventHandler? PropertyChanged;

                private protected virtual void OnPropertyChanged(PropertyChangedEventArgs args)
                    => PropertyChanged?.Invoke(this, args);

                public void Subscribe(PropertyChangedEventHandler handler) => PropertyChanged += handler;
            }

            [Notify]
            public partial class Person : CollectionBase
            {
                private string _name = string.Empty;
            }
            """;

        var result = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
        var generated = GeneratorTestHelper.GetGeneratedSource(result.RunResult, "Person.g.cs");
        generated.Should().NotBeNull();
        generated.Should().NotContain("public event PropertyChangedEventHandler?");
        generated.Should().NotContain(": INotifyPropertyChanged");

        using var assemblyStream = new MemoryStream();
        var emit = result.OutputCompilation.Emit(assemblyStream);
        emit.Success.Should().BeTrue(string.Join(Environment.NewLine, emit.Diagnostics));
        var assembly = Assembly.Load(assemblyStream.ToArray());
        var personType = assembly.GetType("Person")!;
        var person = Activator.CreateInstance(personType)!;
        var changed = new List<string>();
        personType.GetMethod("Subscribe")!.Invoke(person, new object[]
        {
            (PropertyChangedEventHandler)((_, args) => changed.Add(args.PropertyName!))
        });
        personType.GetProperty("Name")!.SetValue(person, "Ada");
        changed.Should().Equal("Name");
    }

    [Fact]
    public void Generator_HostsExistingInpcWithStringInvokerAndNotifiesDependencies()
    {
        const string source = """
            using System.ComponentModel;
            using NotifyGen;

            public abstract class Base : INotifyPropertyChanged
            {
                public event PropertyChangedEventHandler? PropertyChanged;

                protected virtual void OnPropertyChanged(string? propertyName = null)
                    => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

                public void Subscribe(PropertyChangedEventHandler handler) => PropertyChanged += handler;
            }

            [Notify]
            public partial class Person : Base
            {
                [NotifyAlso(nameof(DisplayName))]
                private string _name = string.Empty;

                public string DisplayName => Name;
            }
            """;

        var result = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
        var generated = GeneratorTestHelper.GetGeneratedSource(result.RunResult, "Person.g.cs");
        generated.Should().NotBeNull();
        generated.Should().NotContain("public event PropertyChangedEventHandler?");
        generated.Should().NotContain("protected virtual void OnPropertyChanged");
        generated.Should().Contain("OnPropertyChanged(\"Name\")");

        using var assemblyStream = new MemoryStream();
        result.OutputCompilation.Emit(assemblyStream).Success.Should().BeTrue();
        var assembly = Assembly.Load(assemblyStream.ToArray());
        var personType = assembly.GetType("Person")!;
        var person = Activator.CreateInstance(personType)!;
        var changed = new List<string>();
        personType.GetMethod("Subscribe")!.Invoke(person, new object[]
        {
            (PropertyChangedEventHandler)((_, args) => changed.Add(args.PropertyName!))
        });
        personType.GetProperty("Name")!.SetValue(person, "Ada");
        changed.Should().Equal("Name", "DisplayName");
    }

    [Fact]
    public void Generator_ExplicitInpcWithoutInvoker_WithholdsGeneration()
    {
        const string source = """
            using System.ComponentModel;
            using NotifyGen;

            public abstract class Base : INotifyPropertyChanged
            {
                event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
                {
                    add { }
                    remove { }
                }
            }

            [Notify]
            public partial class Person : Base
            {
                private string _name = string.Empty;
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .Should().BeEmpty();
        result.RunResult.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_NotifyOnCollectionChanged_SupportsPartialPropertyMode()
    {
        const string source = """
            using System.Collections.ObjectModel;
            using NotifyGen;

            [Notify]
            public partial class Basket
            {
                [NotifyAlso(nameof(Count), NotifyOnCollectionChanged = true)]
                public partial ObservableCollection<string> Items { get; set; }

                public int Count => Items.Count;
            }
            """;

        var result = GeneratorTestHelper.RunGeneratorWithLanguageVersionAndAssertCompiles(
            source,
            Microsoft.CodeAnalysis.CSharp.LanguageVersion.Preview
        );
        var generated = GeneratorTestHelper.GetGeneratedSource(result.RunResult, "Basket.g.cs");
        generated.Should().Contain("CollectionChanged +=");
        generated.Should().Contain(
            "partial System.Collections.ObjectModel.ObservableCollection<string> Items"
        );
    }

    [Fact]
    public void Generator_HostSuppressableRoutesThroughExistingInvoker()
    {
        const string source = """
            using System;
            using System.ComponentModel;
            using NotifyGen;

            public abstract class Base : INotifyPropertyChanged
            {
                public event PropertyChangedEventHandler? PropertyChanged;

                protected virtual void OnPropertyChanged(PropertyChangedEventArgs args)
                    => PropertyChanged?.Invoke(this, args);
            }

            [Notify]
            [NotifySuppressable]
            public partial class Person : Base
            {
                private string _name = string.Empty;
            }
            """;

        var result = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
        using var assemblyStream = new MemoryStream();
        result.OutputCompilation.Emit(assemblyStream).Success.Should().BeTrue();
        var assembly = Assembly.Load(assemblyStream.ToArray());
        var type = assembly.GetType("Person")!;
        var person = Activator.CreateInstance(type)!;
        var changed = new List<string>();
        ((INotifyPropertyChanged)person).PropertyChanged += (_, args) => changed.Add(args.PropertyName!);
        using ((IDisposable)type.GetMethod("SuppressNotifications")!.Invoke(person, null)!)
        {
            type.GetProperty("Name")!.SetValue(person, "Ada");
            changed.Should().BeEmpty();
        }
        changed.Should().Equal("Name");
    }

    [Fact]
    public void Generator_NotifyOnCollectionChanged_RaisesDeclaredDependentAndDetachesReplacement()
    {
        const string source = """
            using System.Collections.ObjectModel;
            using System.ComponentModel;
            using NotifyGen;

            [Notify]
            public partial class Basket
            {
                [NotifyAlso(nameof(Count), NotifyOnCollectionChanged = true)]
                [NotifyAlso(nameof(Count), NotifyOnCollectionChanged = true)]
                private ObservableCollection<string> _items = new();

                public int Count => Items.Count;

                public void Replace(ObservableCollection<string> items) => Items = items;
            }
            """;

        var result = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
        var generated = GeneratorTestHelper.GetGeneratedSource(result.RunResult, "Basket.g.cs");
        generated.Should().NotBeNull();
        generated.Should().Contain("INotifyCollectionChanged");
        generated.Should().Contain("CollectionChanged +=");
        generated.Should().Contain("CollectionChanged -=");

        using var assemblyStream = new MemoryStream();
        result.OutputCompilation.Emit(assemblyStream).Success.Should().BeTrue();
        var assembly = Assembly.Load(assemblyStream.ToArray());
        var type = assembly.GetType("Basket")!;
        var basket = Activator.CreateInstance(type)!;
        var changed = new List<string>();
        ((INotifyPropertyChanged)basket).PropertyChanged += (_, args) => changed.Add(args.PropertyName!);
        var items = (ObservableCollection<string>)type.GetProperty("Items")!.GetValue(basket)!;
        items.Add("one");
        changed.Should().Equal("Count");
        changed.Clear();
        var replacement = new ObservableCollection<string>();
        type.GetMethod("Replace")!.Invoke(basket, new object[] { replacement });
        changed.Clear();
        items.Add("stale");
        changed.Should().BeEmpty();
        replacement.Add("two");
        changed.Should().Equal("Count");
    }
}
