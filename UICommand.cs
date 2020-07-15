using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
namespace OdeSolverWPF
{
    public class UICommand : ICommand
    {
        Action action;
        bool executable = false;

        public Boolean Executable
        {
            get { return executable; }
            set
            {
                if (executable!=value)
                {
                    executable = value;
                    if (CanExecuteChanged != null)
                        CanExecuteChanged(this, EventArgs.Empty);
                }
            }
        }

        public UICommand(Action Action)
        {
            action = Action;
        }

        public bool CanExecute(object parameter)
        {
            return executable;
        }

        public event EventHandler CanExecuteChanged;

        public void Execute(object parameter)
        {
            action();
        }
    }
}
