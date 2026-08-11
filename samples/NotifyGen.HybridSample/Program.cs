using NotifyGen.HybridSample;

Console.WriteLine("NotifyGen + CommunityToolkit.Mvvm hybrid sample");
Console.WriteLine("NotifyGen owns INPC; CommunityToolkit owns RelayCommand.\n");

var vm = new EditorViewModel();
vm.PropertyChanged += (_, e) =>
    Console.WriteLine($"  PropertyChanged: {e.PropertyName}");

Console.WriteLine($"CanSave={vm.CanSave}, SaveCommand.CanExecute={vm.SaveCommand.CanExecute(null)}");

vm.Title = "Hello NotifyGen";
Console.WriteLine($"CanSave={vm.CanSave}, SaveCommand.CanExecute={vm.SaveCommand.CanExecute(null)}");

vm.Body = "Zero-runtime INPC + CT commands.";
Console.WriteLine($"CanSave={vm.CanSave}, SaveCommand.CanExecute={vm.SaveCommand.CanExecute(null)}");

if (vm.SaveCommand.CanExecute(null))
    vm.SaveCommand.Execute(null);

Console.WriteLine($"Status: {vm.Status}");
