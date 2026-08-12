using System;

namespace NotifyGen;

/// <summary>
/// Opts a field or incomplete partial property into generation when the
/// containing <see cref="NotifyAttribute"/> type uses field-level opt-in.
/// Presence of this attribute (or CommunityToolkit <c>[ObservableProperty]</c>)
/// on any member switches the type from generate-all-underscore-fields to
/// generate-only-marked-members.
/// </summary>
[AttributeUsage(
    AttributeTargets.Field | AttributeTargets.Property,
    Inherited = false,
    AllowMultiple = false
)]
public sealed class NotifyPropertyAttribute : Attribute { }
