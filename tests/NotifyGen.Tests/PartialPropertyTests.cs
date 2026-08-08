using System.ComponentModel;
using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace NotifyGen.Tests;

public class PartialPropertyTests
{
    [Fact]
    public void PartialProperty_WithExistingImplementation_IsNotDuplicated()
    {
        const string source = """
            using NotifyGen;

            [Notify]
            public partial class PlainEntity
            {
                public partial string Name { get; set; }
            }

            public partial class PlainEntity
            {
                public partial string Name { get => field; set => field = value; }
            }
            """;

        var result = GeneratorTestHelper.RunGeneratorWithLanguageVersionAndAssertCompiles(
            source,
            LanguageVersion.Preview
        );

        result.RunResult.Results.Single().GeneratedSources.Should().BeEmpty();
    }

    [Fact]
    public void PartialProperty_WithUnsupportedStaticModifier_IsIgnored()
    {
        const string source = """
            using NotifyGen;

            [Notify]
            public partial class PlainEntity
            {
                public static partial int Count { get; set; }
            }
            """;

        var result = GeneratorTestHelper.RunGeneratorWithLanguageVersion(
            source,
            LanguageVersion.Preview
        );

        result.RunResult.Results.Single().GeneratedSources.Should().BeEmpty();
    }

    [Fact]
    public void PartialProperty_GeneratesSourceAndRunsWithoutFrameworkBase()
    {
        const string source = """
            using NotifyGen;
            using System;

            namespace PartialPropertyFixture;

            public class FrameworkEntity
            {
                public int FrameworkValue { get; set; }
            }

            [Notify]
            public partial class PlainEntity : FrameworkEntity
            {
                [NotifyAlso(nameof(DisplayName))]
                public partial string Name { get; set; }

                public partial int Count { get; private set; }

                public string DisplayName => Name.Trim();
                public void UpdateCount(int value) => Count = value;
                public string LastHook { get; private set; } = string.Empty;
                public bool ChangedHookWasCalled { get; private set; }

                partial void OnNameChanging(string oldValue, string newValue)
                {
                    LastHook = $"{oldValue}->{newValue}";
                }

                partial void OnNameChanged()
                {
                    ChangedHookWasCalled = true;
                }
            }
            """;

        var result = GeneratorTestHelper.RunGeneratorWithLanguageVersionAndAssertCompiles(
            source,
            LanguageVersion.Preview
        );
        var warnings = result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning)
            .Select(diagnostic => diagnostic.ToString())
            .ToArray();
        warnings.Should().BeEmpty(string.Join(Environment.NewLine, warnings));

        var generated = GeneratorTestHelper.GetGeneratedSource(result.RunResult, "PlainEntity.g.cs");

        generated.Should().NotBeNull();
        generated.Should().Contain("public partial string Name");
        generated.Should().Contain("public partial int Count");
        generated.Should().Contain("private set");
        generated.Should().Contain("get => field!;");
        generated.Should().Contain("OnNameChanging(field!, value);");
        generated.Should().Contain("OnPropertyChanged(\"DisplayName\")");

        using var assemblyStream = new MemoryStream();
        var emit = result.OutputCompilation.Emit(assemblyStream);
        emit.Success.Should().BeTrue(string.Join(Environment.NewLine, emit.Diagnostics));

        var type = Assembly.Load(assemblyStream.ToArray()).GetType(
            "PartialPropertyFixture.PlainEntity"
        );
        type.Should().NotBeNull();
        var instance = Activator.CreateInstance(type!);
        instance.Should().NotBeNull();

        var changed = new List<string>();
        var handler = new PropertyChangedEventHandler(
            (_, args) => changed.Add(args.PropertyName!)
        );
        type!.GetEvent(nameof(INotifyPropertyChanged.PropertyChanged))!
            .AddEventHandler(instance, handler);

        type.GetProperty("Name")!.SetValue(instance, " Ada ");
        type.GetMethod("UpdateCount")!.Invoke(instance, new object[] { 3 });

        changed.Should().Equal("Name", "DisplayName", "Count");
        type.GetProperty("DisplayName")!.GetValue(instance).Should().Be("Ada");
        type.GetProperty("Count")!.GetValue(instance).Should().Be(3);
        type.GetProperty("LastHook")!.GetValue(instance).Should().Be("-> Ada ");
        type.GetProperty("ChangedHookWasCalled")!.GetValue(instance).Should().Be(true);

        changed.Clear();
        type.GetProperty("Name")!.SetValue(instance, " Ada ");
        changed.Should().BeEmpty("the generated setter retains the equality guard");
    }
}
