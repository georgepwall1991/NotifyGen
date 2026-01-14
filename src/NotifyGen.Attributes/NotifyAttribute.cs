using System;

namespace NotifyGen;

/// <summary>
/// Marks a partial class for INotifyPropertyChanged source generation.
/// The generator will create properties for all private fields with underscore prefix.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class NotifyAttribute : Attribute
{
}
