# Explicit collection membership notifications

`[NotifyAlso(nameof(Count), NotifyOnCollectionChanged = true)]` opts a generated collection property into direct `INotifyCollectionChanged` subscription. The generated code attaches to the current collection, detaches replacements/nulls, and raises the declared dependent targets once for each collection event. It does not subscribe to item `INotifyPropertyChanged`, infer getter dependencies, or proxy arbitrary collections.


The option is source-side only; `NotifyFrom = true` is diagnosed as `NOTIFY014`. Reference-valued sources are checked at runtime, so a custom implementation may be used even when its static type is not a known collection interface. Value sources receive `NOTIFY015` because they cannot implement the event interface. Generated member names are collision-safe, and duplicate collection declarations are deduplicated while preserving declaration order.
