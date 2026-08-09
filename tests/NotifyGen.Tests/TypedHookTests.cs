using System;
using System.Collections.Generic;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace NotifyGen.Tests;

public class TypedHookTests
{
    [Fact]
    public void TypedPostChangeHook_ReceivesOldAndNewValuesAfterParameterlessHook()
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private string _name = "before";
                public List<string> HookEvents { get; } = new();

                public Person()
                {
                    PropertyChanged += (_, _) => HookEvents.Add("PropertyChanged");
                }

                partial void OnNameChanged() => HookEvents.Add("parameterless");
                partial void OnNameChanged(string oldValue, string newValue) =>
                    HookEvents.Add($"typed:{oldValue}->{newValue}");
            }
            """;

        var result = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
        using var assemblyStream = new System.IO.MemoryStream();
        result.OutputCompilation.Emit(assemblyStream).Success.Should().BeTrue();
        var personType = Assembly.Load(assemblyStream.ToArray()).GetType("Person")!;
        var person = Activator.CreateInstance(personType)!;
        personType.GetProperty("Name")!.SetValue(person, "after");

        var events = (List<string>)personType.GetProperty("HookEvents")!.GetValue(person)!;
        events.Should().Equal("PropertyChanged", "parameterless", "typed:before->after");

        events.Clear();
        personType.GetProperty("Name")!.SetValue(person, "after");
        events.Should().BeEmpty();
    }

    [Fact]
    public void TypedPostChangeHook_WorksForPartialPropertyMode()
    {
        const string source = """
            using System.Collections.Generic;
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                public List<string> HookEvents { get; } = new();

                public partial string Name { get; set; }

                partial void OnNameChanged(string oldValue, string newValue) =>
                    HookEvents.Add($"{oldValue}->{newValue}");
            }
            """;

        var result = GeneratorTestHelper.RunGeneratorWithLanguageVersionAndAssertCompiles(
            source,
            Microsoft.CodeAnalysis.CSharp.LanguageVersion.Preview
        );
        using var assemblyStream = new System.IO.MemoryStream();
        result.OutputCompilation.Emit(assemblyStream).Success.Should().BeTrue();
        var personType = Assembly.Load(assemblyStream.ToArray()).GetType("Person")!;
        var person = Activator.CreateInstance(personType)!;
        personType.GetProperty("Name")!.SetValue(person, "after");

        var events = (List<string>)personType.GetProperty("HookEvents")!.GetValue(person)!;
        events.Should().Equal("->after");
    }
    [Fact]
    public void TypedPostChangeHook_UsesNonCollidingOldValueLocal()
    {
        const string source = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private int __notifyGenOldValue;
            }
            """;

        var result = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
        using var assemblyStream = new System.IO.MemoryStream();
        result.OutputCompilation.Emit(assemblyStream).Success.Should().BeTrue();
    }

    [Fact]
    public void TypedPostChangeHook_DynamicValuesEraseWhenUnimplemented()
    {
        const string source = """
            using System.Collections.Generic;
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private dynamic? _value = 1;
                public List<string> HookEvents { get; } = new();
            }
            """;

        var result = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
        using var assemblyStream = new System.IO.MemoryStream();
        result.OutputCompilation.Emit(assemblyStream).Success.Should().BeTrue();
        var personType = Assembly.Load(assemblyStream.ToArray()).GetType("Person")!;
        var person = Activator.CreateInstance(personType)!;
        personType.GetProperty("Value")!.SetValue(person, 2);

        var events = (List<string>)personType.GetProperty("HookEvents")!.GetValue(person)!;
        events.Should().BeEmpty();
    }

    [Fact]
    public void TypedPostChangeHook_DynamicValuesSupportImplementedHooksWithoutWarnings()
    {
        const string source = """
            using System.Collections.Generic;
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private dynamic _value = 1;
                public List<string> HookEvents { get; } = new();

                partial void OnValueChanging(dynamic oldValue, dynamic newValue) { }
                partial void OnValueChanged(dynamic oldValue, dynamic newValue) =>
                    HookEvents.Add($"changed:{oldValue}->{newValue}");
            }
            """;

        var result = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
        result.OutputCompilation.GetDiagnostics().Should().BeEmpty();
        using var assemblyStream = new System.IO.MemoryStream();
        result.OutputCompilation.Emit(assemblyStream).Success.Should().BeTrue();
        var personType = Assembly.Load(assemblyStream.ToArray()).GetType("Person")!;
        var person = Activator.CreateInstance(personType)!;
        personType.GetProperty("Value")!.SetValue(person, 2);

        var events = (List<string>)personType.GetProperty("HookEvents")!.GetValue(person)!;
        events.Should().Equal("changed:1->2");
    }

    [Fact]
    public void TypedPostChangeHook_PreservesOrdinaryMethodImplementations()
    {
        const string source = """
            using System.Collections.Generic;
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private int _value;
                public List<string> HookEvents { get; } = new();

                private void OnValueChanged(int oldValue, int newValue) =>
                    HookEvents.Add($"{oldValue}->{newValue}");
            }
            """;

        var result = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
        using var assemblyStream = new System.IO.MemoryStream();
        result.OutputCompilation.Emit(assemblyStream).Success.Should().BeTrue();
        var personType = Assembly.Load(assemblyStream.ToArray()).GetType("Person")!;
        var person = Activator.CreateInstance(personType)!;
        personType.GetProperty("Value")!.SetValue(person, 2);

        var events = (List<string>)personType.GetProperty("HookEvents")!.GetValue(person)!;
        events.Should().Equal("0->2");
    }

    [Fact]
    public void TypedPostChangeHook_ReusesNestedMetadataEquivalentOrdinaryMethod()
    {
        const string source = """
            using System.Collections.Generic;
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private List<dynamic> _value = new();
                public List<string> HookEvents { get; } = new();

                private void OnValueChanged(List<object> oldValue, List<object> newValue) =>
                    HookEvents.Add($"{oldValue.Count}->{newValue.Count}");
            }
            """;

        var result = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
        result.OutputCompilation.GetDiagnostics().Should().BeEmpty();
        using var assemblyStream = new System.IO.MemoryStream();
        result.OutputCompilation.Emit(assemblyStream).Success.Should().BeTrue();
        var personType = Assembly.Load(assemblyStream.ToArray()).GetType("Person")!;
        var person = Activator.CreateInstance(personType)!;
        personType.GetProperty("Value")!.SetValue(person, new List<object> { 1 });

        var events = (List<string>)personType.GetProperty("HookEvents")!.GetValue(person)!;
        events.Should().Equal("0->1");
    }

    [Fact]
    public void TypedPostChangeHook_HandlesPerParameterNullabilityDifferences()
    {
        const string source = """
            using System.Collections.Generic;
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private List<string?> _value = new();
                public List<string> HookEvents { get; } = new();

                private void OnValueChanged(List<string> oldValue, List<string?> newValue) =>
                    HookEvents.Add($"{oldValue.Count}->{newValue.Count}");
            }
            """;

        var result = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
        result.OutputCompilation.GetDiagnostics().Should().BeEmpty();
        using var assemblyStream = new System.IO.MemoryStream();
        result.OutputCompilation.Emit(assemblyStream).Success.Should().BeTrue();
        var personType = Assembly.Load(assemblyStream.ToArray()).GetType("Person")!;
        var person = Activator.CreateInstance(personType)!;
        personType.GetProperty("Value")!.SetValue(person, new List<string> { "x" });

        var events = (List<string>)personType.GetProperty("HookEvents")!.GetValue(person)!;
        events.Should().Equal("0->1");
    }

    [Fact]
    public void TypedPostChangeHook_SuppressesNestedNullableMismatch()
    {
        const string source = """
            using System.Collections.Generic;
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private List<string?> _value = new();
                public List<string> HookEvents { get; } = new();

                private void OnValueChanged(List<string> oldValue, List<string> newValue) =>
                    HookEvents.Add($"{oldValue.Count}->{newValue.Count}");
            }
            """;

        var result = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
        result.OutputCompilation.GetDiagnostics().Should().BeEmpty();
        using var assemblyStream = new System.IO.MemoryStream();
        result.OutputCompilation.Emit(assemblyStream).Success.Should().BeTrue();
        var personType = Assembly.Load(assemblyStream.ToArray()).GetType("Person")!;
        var person = Activator.CreateInstance(personType)!;
        personType.GetProperty("Value")!.SetValue(person, new List<string> { "x" });

        var events = (List<string>)personType.GetProperty("HookEvents")!.GetValue(person)!;
        events.Should().Equal("0->1");
    }

    [Fact]
    public void TypedPostChangeHook_SuppressesNullableMismatchForOrdinaryMethod()
    {
        const string source = """
            using System.Collections.Generic;
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private string? _value;
                public List<string> HookEvents { get; } = new();

                private void OnValueChanged(string oldValue, string newValue) =>
                    HookEvents.Add($"{oldValue ?? "<null>"}->{newValue}");
            }
            """;

        var result = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
        result.OutputCompilation.GetDiagnostics().Should().BeEmpty();
        using var assemblyStream = new System.IO.MemoryStream();
        result.OutputCompilation.Emit(assemblyStream).Success.Should().BeTrue();
        var personType = Assembly.Load(assemblyStream.ToArray()).GetType("Person")!;
        var person = Activator.CreateInstance(personType)!;
        personType.GetProperty("Value")!.SetValue(person, "after");

        var events = (List<string>)personType.GetProperty("HookEvents")!.GetValue(person)!;
        events.Should().Equal("<null>->after");
    }

    [Fact]
    public void TypedPostChangeHook_ReusesAccessibleInheritedMethod()
    {
        const string source = """
            using System.Collections.Generic;
            using NotifyGen;

            public class BasePerson
            {
                protected List<string> HookEvents { get; } = new();

                protected void OnValueChanged(int oldValue, int newValue) =>
                    HookEvents.Add($"{oldValue}->{newValue}");

                public IReadOnlyList<string> Events => HookEvents;
            }

            [Notify]
            public partial class Person : BasePerson
            {
                private int _value;
            }
            """;

        var result = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
        using var assemblyStream = new System.IO.MemoryStream();
        result.OutputCompilation.Emit(assemblyStream).Success.Should().BeTrue();
        var personType = Assembly.Load(assemblyStream.ToArray()).GetType("Person")!;
        var person = Activator.CreateInstance(personType)!;
        personType.GetProperty("Value")!.SetValue(person, 2);

        var events = (IReadOnlyList<string>)personType.GetProperty("Events")!.GetValue(person)!;
        events.Should().Equal("0->2");
    }

    [Fact]
    public void TypedPostChangeHook_ReusesObjectMethodForDynamicProperty()
    {
        const string source = """
            using System.Collections.Generic;
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private dynamic _value = 1;
                public List<string> HookEvents { get; } = new();

                private void OnValueChanged(object oldValue, object newValue) =>
                    HookEvents.Add($"{oldValue}->{newValue}");
            }
            """;

        var result = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
        result.OutputCompilation.GetDiagnostics().Should().BeEmpty();
        using var assemblyStream = new System.IO.MemoryStream();
        result.OutputCompilation.Emit(assemblyStream).Success.Should().BeTrue();
        var personType = Assembly.Load(assemblyStream.ToArray()).GetType("Person")!;
        var person = Activator.CreateInstance(personType)!;
        personType.GetProperty("Value")!.SetValue(person, 2);

        var events = (List<string>)personType.GetProperty("HookEvents")!.GetValue(person)!;
        events.Should().Equal("1->2");
    }

    [Fact]
    public void TypedPostChangeHook_DoesNotTreatRefOverloadAsTypedImplementation()
    {
        const string source = """
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                private int _value;

                private void OnValueChanged(ref int oldValue, int newValue) { }
            }
            """;

        var result = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
        using var assemblyStream = new System.IO.MemoryStream();
        result.OutputCompilation.Emit(assemblyStream).Success.Should().BeTrue();
    }

}
