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

using OxyPlot;
using OxyPlot.Series;

namespace OdeSolverWPF
{
    /// <summary>
    /// Interaction logic for PlotForm.xaml
    /// </summary>
    public partial class PlotForm : Window
    {
        public PlotForm()
        {
            InitializeComponent();
            tmp = new PlotModel {LegendBackground = OxyColors.LightGray};         
            PlotView.Model = tmp;
        }
        PlotModel tmp;

        public void AddSeries(String Name, Double[] X, Double[] Y)
        {
            var series = new LineSeries { Title = Name, MarkerType = MarkerType.None, StrokeThickness = 1.5 };

            for (int i = 0; i < X.Length; i++)
                series.Points.Add(new DataPoint(X[i],Y[i]));

            tmp.Series.Add(series);

        }


    }
}
