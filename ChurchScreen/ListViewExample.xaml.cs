using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    /// Логика взаимодействия для ListViewExample.xaml
    /// </summary>
    public partial class ListViewExample : Window
    {
        public ListViewExample()
        {
            InitializeComponent();
        }

        private void addItemButton_Click(object sender, RoutedEventArgs e)
        {
            this.listView.Items.Add(new SearchItem { Id = 1, SongName = "David" });
        }

        private void removeItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (listView.Items.Count != 0)
                listView.Items.RemoveAt(0);
        }
    }
}
