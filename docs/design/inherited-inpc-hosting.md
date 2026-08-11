# Hosting an existing INPC implementation

`[Notify]` may be applied to a partial type whose base type already implements `INotifyPropertyChanged`. NotifyGen does not emit a second interface, event, or helper. It discovers an accessible ordinary instance `OnPropertyChanged(string)` (including an optional parameter) or `OnPropertyChanged(PropertyChangedEventArgs)` method and emits calls to that helper. A derived declaration that hides an otherwise compatible base method is not treated as callable.

If no compatible helper is accessible, the analyzer reports `NOTIFY013` and generation is withheld. When `[Notify(ImplementChanging = true)]` reuses an existing `INotifyPropertyChanging` implementation, the equivalent `OnPropertyChanging(string)`/`OnPropertyChanging(PropertyChangingEventArgs)` host is required; `NOTIFY017` protects that path.

`[NotifySuppressable]` is supported with a host through a private generated forwarding method. Setter, child, and collection notifications enter the suppression queue through that method, while resumed notifications invoke the host helper. The generator never invokes an inherited event directly, adds reflection, or introduces a runtime package.
