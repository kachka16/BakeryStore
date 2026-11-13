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

namespace StoreBakery
{
    /// <summary>
    /// Interaction logic for LoginKasaWindow.xaml
    /// </summary>
    public partial class LoginKasaWindow : Window
    {
        public LoginKasaWindow()
        {
            InitializeComponent();
        }
        private void authButtonBack(object sender, RoutedEventArgs e)
        {
            AutoWindows autoWind = new AutoWindows();
            autoWind.Show();
            this.Hide();
        }
        private void authButtonKasa(object sender, RoutedEventArgs e)
        {
            string login = textBoxLogin.Text.Trim();
            string password = passBox.Password.Trim();

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Будь ласка, заповніть всі поля!");
                return;
            }


            using (ApplicationContext db = new ApplicationContext())
            {
                var authKasa = db.Kasa.FirstOrDefault(x => x.login == login && x.pass == password);

                if (authKasa != null)
                {
                    MessageBox.Show("Все добре! Успішний вхід.");
                    KasaSaleWindow kasaWindow = new KasaSaleWindow();
                    kasaWindow.Show();
                    this.Hide();
                }
                else
                {
                    MessageBox.Show("Невірний логін, ID або пароль!");
                }
            }
        }
    }
}
