using System;
using System.Windows.Input;

namespace MonBand.Windows.Infrastructure.Input;

public class DelegateCommand : ICommand
{
    readonly Action<object?> _execute;
    readonly Func<object?, bool> _canExecute;

    public DelegateCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        this._execute = execute;
        this._canExecute = canExecute ?? (_ => true);
    }

    public bool CanExecute(object? parameter)
    {
        return this._canExecute(parameter);
    }

    public void Execute(object? parameter)
    {
        this._execute(parameter);
    }

    #pragma warning disable CS0067
    public event EventHandler? CanExecuteChanged;
    #pragma warning restore CS0067
}
