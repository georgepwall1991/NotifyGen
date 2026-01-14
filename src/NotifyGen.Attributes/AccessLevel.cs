namespace NotifyGen;

/// <summary>
/// Specifies the accessibility level for generated property accessors.
/// </summary>
public enum AccessLevel
{
    /// <summary>
    /// Public accessibility (default).
    /// </summary>
    Public = 0,

    /// <summary>
    /// Protected accessibility.
    /// </summary>
    Protected = 1,

    /// <summary>
    /// Internal accessibility.
    /// </summary>
    Internal = 2,

    /// <summary>
    /// Private accessibility.
    /// </summary>
    Private = 3,

    /// <summary>
    /// Protected internal accessibility.
    /// </summary>
    ProtectedInternal = 4,

    /// <summary>
    /// Private protected accessibility.
    /// </summary>
    PrivateProtected = 5
}
