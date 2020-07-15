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
using System.Windows.Shapes;
using System.Data;

namespace OdeSolverWPF
{
    /// <summary>
    /// Interaction logic for TableForm.xaml
    /// </summary>
    public partial class TableForm : Window
    {
        public TableForm()
        {
            InitializeComponent();

        }


        public void AddRow(String RowName,Double[] RowValues)
        {
            for (int i = Table.Columns.Count; i < RowValues.Length; i++)
                Table.Columns.Add(new MyColumn() { Index = i});

            MyrowData rd = new MyrowData();
            rd.RowId = RowName;
            rd.Data = RowValues;
            Table.Items.Add(rd);

        }
    }

    public class MyrowData
    {
        public String RowId { get; set; }
        public Double[] Data { get; set; }
    }
    public class MyColumn : DataGridColumn
    {
        public MyColumn()
        {
            IsReadOnly = true;
        }
        int index = 0;

        public int Index
        {
            get { return index; }
            set { index = value; Header = value.ToString(); }
        }
 
        protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem)
        {
            throw new NotImplementedException();
        }

        protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
        {
            TextBlock tb = new TextBlock();
            MyrowData D = dataItem as MyrowData;

            if (D != null && Index < D.Data.Length)
                tb.Text = D.Data[Index].ToString("F5");

            return tb;

        }
    }
}
