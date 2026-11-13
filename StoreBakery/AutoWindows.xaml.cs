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
using System.Windows.Shapes;

namespace StoreBakery
{
    /// <summary>
    /// Interaction logic for AutoWindows.xaml
    /// </summary>
    public partial class AutoWindows : Window
    {
        public AutoWindows()
        {
            InitializeComponent();
            DoubleAnimation choseAnim = new DoubleAnimation();
            choseAnim.From = 0;
            choseAnim.To = 405;
            choseAnim.Duration = TimeSpan.FromSeconds(1);
            choseRole.BeginAnimation(Button.WidthProperty, choseAnim);
        }
        private void AdminLog(object sender, MouseButtonEventArgs e)
        {
            LoginAdminWindow logWind  = new LoginAdminWindow();
            logWind.Show();
            this.Hide();
        }
        private void KasaLog(object sender, MouseButtonEventArgs e)
        {
            LoginKasaWindow logkasaWind = new LoginKasaWindow();
            logkasaWind.Show();
            this.Hide();
        }
    }
}
