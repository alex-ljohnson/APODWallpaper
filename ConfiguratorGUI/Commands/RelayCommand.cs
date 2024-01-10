
using System.Windows.Input;

namespace ConfiguratorGUI
{
    class RelayCommand(Action execute, Predicate<object?> canExecute) : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return canExecute == null || canExecute(parameter);
        }

        public void Execute(object? parameter)
        {
            execute?.Invoke();
        }
    }
    class RelayCommand<T>(Action<T?> execute, Predicate<T?> canExecute) : ICommand
    {

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return canExecute == null || canExecute((T?)parameter);
        }

        public void Execute(object? parameter)
        {
            execute?.Invoke((T?)parameter);
        }
    }
}
