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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace StoreBakery
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DoubleAnimation btnAnimation = new DoubleAnimation();
            btnAnimation.From = 0;
            btnAnimation.To = 320;
            btnAnimation.Duration = TimeSpan.FromSeconds(1);
            bthMain.BeginAnimation(Button.WidthProperty, btnAnimation);
        }

        private void AutoWind(object sender, RoutedEventArgs e)
        {
            AutoWindows autoWind = new AutoWindows();
            autoWind.Show();
            this.Hide();
        }
    }
}
