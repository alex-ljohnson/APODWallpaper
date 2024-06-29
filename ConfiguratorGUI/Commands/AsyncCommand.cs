using System.Windows.Input;

namespace ConfiguratorGUI.Commands
{
    internal interface IAsyncCommand : ICommand
    {
        Task ExecuteAsync(object? parameter);
        new bool CanExecute(object? parameter);
    }

    internal class AsyncCommand(Func<Task> execute, Predicate<object?> canExecute, bool isExecuting) : IAsyncCommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return !isExecuting && (canExecute?.Invoke(parameter) ?? true);
        }

        public void Execute(object? parameter)
        {
            throw new NotImplementedException();
        }

        public async Task ExecuteAsync(object? parameter)
        {
            if (CanExecute(parameter))
            {
                try
                {
                    isExecuting = true;
                    await execute();
                }
                finally
                {
                    isExecuting = false;
                }
            }

            CanExecuteChanged?.Invoke(this, new EventArgs());
        }
    }

    internal class AsyncCommand<T>(Func<T?, Task> execute, Predicate<T?> canExecute, bool isExecuting) : IAsyncCommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return !isExecuting && (canExecute?.Invoke((T?)parameter) ?? true);
        }

        public void Execute(object? parameter)
        {
            throw new NotImplementedException();
        }

        public async Task ExecuteAsync(object? parameter)
        {
            if (CanExecute(parameter))
            {
                try
                {
                    isExecuting = true;
                    await execute((T?)parameter);
                }
                finally
                {
                    isExecuting = false;
                }
            }

            CanExecuteChanged?.Invoke(this, new EventArgs());

        }
    }
}
