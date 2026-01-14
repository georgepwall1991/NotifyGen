using System;

namespace NotifyGen;

/// <summary>
/// Excludes a field from property generation.
/// Use this for backing fields that are managed manually.
/// </summary>
[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class NotifyIgnoreAttribute : Attribute
{
}
