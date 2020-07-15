using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace OdeSolverWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private Brush DefaultBrush = new SolidColorBrush(Color.FromArgb(255, 51, 153, 255)) {Opacity = 0.7};
        private String currentFileName;
        private bool IsHighlighted = false;


        public UICommand CustomCommand { get; private set; }

        public MainWindow()
        {
            InitializeComponent();

            CustomCommand = new UICommand(Parse);
            CustomCommand.Executable = true;

            KeyBinding ParseBind = new KeyBinding(CustomCommand, new KeyGesture(Key.F5));
            this.InputBindings.Add(ParseBind);

            ParseMenuItem.Command = CustomCommand;

            Editor.TextArea.SelectionChanged += TextArea_SelectionChanged;
            Editor.TextArea.SelectionCornerRadius = 0;
            Editor.TextArea.SelectionBrush = DefaultBrush;
            Editor.TextArea.IndentationStrategy = new ICSharpCode.AvalonEdit.Indentation.CSharp.CSharpIndentationStrategy();

        }

        void TextArea_SelectionChanged(object sender, EventArgs e)
        {
            if (IsHighlighted)
            {
                IsHighlighted = false;
                Editor.TextArea.SelectionBrush = DefaultBrush;
                Editor.TextArea.SelectionBorder.Brush = DefaultBrush;

            }
        }


        private void HighlightError(ErrorWord W)
        {         
            Editor.TextArea.SelectionBrush = new SolidColorBrush(Colors.Red);
            Editor.TextArea.SelectionBorder.Brush = new SolidColorBrush(Colors.Red);
            if (W.Length != 0)
                Editor.Select(W.StartIndex, W.Length);
            else
            {
                if (!Editor.Text.EndsWith(" ")) 
                    Editor.Text += " ";
                Editor.Select(Editor.Text.Length-1, 1);
            }
            IsHighlighted = true;


        }

        private void FileItem_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.CheckFileExists = true;
            dlg.Filter = "Текст|*.txt";
            if (dlg.ShowDialog() ?? false)
            {
                currentFileName = dlg.FileName;
                Editor.Load(currentFileName);
            }
        }

        private void Parse()
        {

            Parser P = new Parser();
            try
            {
                P.Execute(Editor.Text);

                SB.Background = new SolidColorBrush(Colors.Green);
                SB.Foreground = new SolidColorBrush(Colors.White);
                Status.Text = "Программа выполнена успешно";
            }
            catch (ParserException E)
            {
                SB.Background = new SolidColorBrush(Colors.Red);
                SB.Foreground = new SolidColorBrush(Colors.White);
                ErrorWord W = P.GetError();
                if (String.IsNullOrEmpty(W.Word))
                    Status.Text = E.Message;
                else
                    Status.Text = String.Format("Ошибка в слове '{0}' : ", W.Word) + E.Message;
                HighlightError(W);
            }
        }

        private void SaveFileItem_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            if (dlg.ShowDialog() ?? false)
            {
                dlg.Filter = "Текст|*.txt";
                Editor.Save(dlg.FileName);
            }
        }

    }
}
