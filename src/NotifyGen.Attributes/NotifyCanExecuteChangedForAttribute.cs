using System;

namespace NotifyGen;

/// <summary>
/// Specifies that changing this property should also call NotifyCanExecuteChanged()
/// on the specified command property. Use this to refresh command CanExecute status
/// when dependent properties change.
/// </summary>
/// <remarks>
/// The target command must implement IRelayCommand or have a NotifyCanExecuteChanged() method.
/// Multiple attributes can be applied to notify multiple commands.
/// </remarks>
/// <example>
/// <code>
/// [Notify]
/// public partial class ViewModel
/// {
///     [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
///     [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
///     private string _name;
///
///     public IRelayCommand SaveCommand { get; }
///     public IRelayCommand DeleteCommand { get; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
public sealed class NotifyCanExecuteChangedForAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the command property to notify.
    /// </summary>
    public string CommandName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotifyCanExecuteChangedForAttribute"/> class.
    /// </summary>
    /// <param name="commandName">The name of the command property to notify when this property changes.</param>
    public NotifyCanExecuteChangedForAttribute(string commandName)
    {
        CommandName = commandName;
    }
}
