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

namespace ChurchScreen
{
    /// <summary>
    /// Логика взаимодействия для ShowScreen.xaml
    /// </summary>
    public partial class ShowScreen : Window
    {
        public ShowScreen()
        {
            InitializeComponent();
            docViewer.PreviewMouseWheel += (s, e) => e.Handled = true;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {

        }
    }
}
