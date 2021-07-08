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
using System.Windows.Forms;
using System.IO;
using System.Threading;

namespace ChurchScreen
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public SongDocument song;
        public FoundSong foundSong;
        public Configuration config;
        public ShowScreen sh;
        public ListViewExample lve;
        public int ScreenWidth;        

        public bool SongSaved = true;//сохранена ли песня перед выходом?

        public MainWindow()
        {
            InitializeComponent();
            try
            {
                if (File.Exists(Environment.CurrentDirectory + "\\settings.xml"))
                {
                    //Считывание настроек системы из файла settings.xml
                    System.Xml.Serialization.XmlSerializer reader =
                        new System.Xml.Serialization.XmlSerializer(typeof(Configuration));
                    System.IO.StreamReader file = new System.IO.StreamReader(Environment.CurrentDirectory + "\\settings.xml");
                    config = (Configuration)reader.Deserialize(file);
                    file.Close();
                }
                else
                {
                    config = new Configuration { FontSizeStep = 5, AlwaysServiceMode = false, SaveAsk = true, UseOneMonitor = false, StrechFill = 2 };
                }
            }
            catch (Exception ex)
            {
                config = new Configuration { FontSizeStep = 5, AlwaysServiceMode = false, SaveAsk = true, UseOneMonitor = false, StrechFill = 2 };
            }
        }       

        private void Window_Loaded_1(object sender, RoutedEventArgs e)
        {
            int upperBound;

            // Gets an array of all the screens connected to the system.
            Screen[] screens = Screen.AllScreens;
            upperBound = screens.GetUpperBound(0);

            string[] files = System.IO.Directory.GetFiles("pictures");

            for (int x = 0; x < files.Length; x++)
            {
                backgroundListView.Items.Add(new PicturesFileName(files[x]));                
            }

            if (backgroundListView.Items.Count != 0) backgroundListView.SelectedIndex = 0;

            //запись xml файла
            /*var b = new Configuration { FontSizeStep = 5, AlwaysServiceMode = false, SaveAsk = true};
            var writer = new System.Xml.Serialization.XmlSerializer(typeof(Configuration));
            var wfile = new System.IO.StreamWriter(Environment.CurrentDirectory + "\\tmp.xml");
            writer.Serialize(wfile, b);
            wfile.Close();*/            

            sh = new ShowScreen();

            if (!config.UseOneMonitor)
            {
                if (screens.Length > 1)
                {
                    sh.Left = screens[1].WorkingArea.X;
                    sh.Top = screens[1].WorkingArea.Y;
                    sh.Width = screens[1].Bounds.Width;
                    sh.Height = screens[1].Bounds.Height;
                    sh.docViewer.Document.ColumnWidth = screens[1].Bounds.Width + 150;
                    ScreenWidth = screens[1].Bounds.Width;
                }
                else
                {
                    sh.Left = screens[0].WorkingArea.X;
                    sh.Top = screens[0].WorkingArea.Y;
                    sh.Width = screens[0].Bounds.Width;
                    sh.Height = screens[0].Bounds.Height;
                    sh.docViewer.Document.ColumnWidth = screens[0].Bounds.Width + 150;
                    ScreenWidth = screens[0].Bounds.Width;
                }
            }
            else
            {
                sh.Left = screens[0].WorkingArea.X;
                sh.Top = screens[0].WorkingArea.Y;
                sh.Width = screens[0].Bounds.Width;
                sh.Height = screens[0].Bounds.Height;
                sh.docViewer.Document.ColumnWidth = screens[0].Bounds.Width + 150;
                ScreenWidth = screens[0].Bounds.Width;
            }


            if (config.AlwaysServiceMode) servicePanel.Visibility = System.Windows.Visibility.Visible;
            else servicePanel.Visibility = System.Windows.Visibility.Hidden;

            String filePic = "pictures\\default.jpg";
            bool noDefaultFile = false;
            if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\" + filePic))
            {
                filePic = "pictures\\default.png";
            }
            else
            {
                noDefaultFile = false;
            }
            if(!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\" + filePic))
            {
                noDefaultFile = true;
            }
            else
            {
                noDefaultFile = false;
            }
            if(!noDefaultFile)
            {
                ImageBrush myBrush = new ImageBrush();
                Image image = new Image();
                image.Source = new BitmapImage(new Uri(AppDomain.CurrentDomain.BaseDirectory + "\\" + filePic, UriKind.Relative));
                myBrush.ImageSource = image.Source;
                if (config.StrechFill == 0)
                    myBrush.Stretch = Stretch.Fill;
                else if (config.StrechFill == 1)
                    myBrush.Stretch = Stretch.Uniform;
                else if (config.StrechFill == 2)
                    myBrush.Stretch = Stretch.UniformToFill;
                sh.mainScreen.Background = myBrush;
            }

            sh.Show();
            this.Activate();
        }
        
        private void Window_Closed_1(object sender, EventArgs e)
        {
            if (!SongSaved)
            {
                if (System.Windows.MessageBox.Show("Редактируемая песня еще не сохранена. Сохранить?", "Сервисный режим", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    saveFileButton_Click(null, null);
            }
            if (sh.IsLoaded)
                sh.Close();
        }

        private void ShowButton_Click_1(object sender, RoutedEventArgs e)
        {
            if (song == null)
            {
                eButton_Click(null, null);
                return;
            }
            sh.mainScreen.Background = null;
            sh.docViewer.Document = song.ToMainScreen();
            double a = sh.docViewer.Document.PageHeight;
            sh.docViewer.MaxZoom = 1000;
            //sh.docViewer.Zoom = 400;
            //sh.docViewer.FontStretch = FontStretches.SemiCondensed;
            double b = sh.docViewer.Document.PageHeight;

            if (song.IsEnd)
            {
                HideDocument_Click(null, null);
                this.songGrid.DataContext = null;
            }
            else
            {
                foreach (Block bl in sh.docViewer.Document.Blocks)
                {
                    this.songGrid.DataContext = bl;
                }
            }

        }

        private void cButton_Click(object sender, RoutedEventArgs e)
        {
            fileNameTextBox.Text = "";
        }        

        private void _8Button_Click(object sender, RoutedEventArgs e)
        {
            if (fileNameTextBox.Text.Length < 4)
            {
                fileNameTextBox.Text += "8";
            }
            else
            {
                fileNameTextBox.Text = "";
                fileNameTextBox.Text += "8";
            }
        }

        private void _7Button_Click(object sender, RoutedEventArgs e)
        {
            if (fileNameTextBox.Text.Length < 4)
            {
                fileNameTextBox.Text += "7";
            }
            else
            {
                fileNameTextBox.Text = "";
                fileNameTextBox.Text += "7";
            }
        }

        private void _9Button_Click(object sender, RoutedEventArgs e)
        {
            if (fileNameTextBox.Text.Length < 4)
            {
                fileNameTextBox.Text += "9";
            }
            else
            {
                fileNameTextBox.Text = "";
                fileNameTextBox.Text += "7";
            }
        }

        private void _4Button_Click(object sender, RoutedEventArgs e)
        {
            if (fileNameTextBox.Text.Length < 4)
            {
                fileNameTextBox.Text += "4";
            }
            else
            {
                fileNameTextBox.Text = "";
                fileNameTextBox.Text += "4";
            }
        }

        private void _5Button_Click(object sender, RoutedEventArgs e)
        {
            if (fileNameTextBox.Text.Length < 4)
            {
                fileNameTextBox.Text += "5";
            }
            else
            {
                fileNameTextBox.Text = "";
                fileNameTextBox.Text += "5";
            }
        }

        private void _6Button_Click(object sender, RoutedEventArgs e)
        {
            if (fileNameTextBox.Text.Length < 4)
            {
                fileNameTextBox.Text += "6";
            }
            else
            {
                fileNameTextBox.Text = "";
                fileNameTextBox.Text += "6";
            }
        }

        private void _1Button_Click(object sender, RoutedEventArgs e)
        {
            if (fileNameTextBox.Text.Length < 4)
            {
                fileNameTextBox.Text += "1";
            }
            else
            {
                fileNameTextBox.Text = "";
                fileNameTextBox.Text += "1";
            }
        }

        private void _2Button_Click(object sender, RoutedEventArgs e)
        {
            if (fileNameTextBox.Text.Length < 4)
            {
                fileNameTextBox.Text += "2";
            }
            else
            {
                fileNameTextBox.Text = "";
                fileNameTextBox.Text += "2";
            }
        }

        private void _3Button_Click(object sender, RoutedEventArgs e)
        {
            if (fileNameTextBox.Text.Length < 4)
            {
                fileNameTextBox.Text += "3";
            }
            else
            {
                fileNameTextBox.Text = "";
                fileNameTextBox.Text += "3";
            }
        }

        private void _0Button_Click(object sender, RoutedEventArgs e)
        {
            if (fileNameTextBox.Text.Length < 4)
            {
                fileNameTextBox.Text += "0";
            }
            else
            {
                fileNameTextBox.Text = "";
                fileNameTextBox.Text += "0";
            }
        }               

        private void Window_KeyDown_1(object sender, System.Windows.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.NumPad0:
                {
                    if (fileNameTextBox.Text.Length < 4)
                    {
                        fileNameTextBox.Text += "0";
                    }
                    else
                    {
                        fileNameTextBox.Text = "";
                        fileNameTextBox.Text += "0";
                    }
                    break;
                }
                case Key.NumPad1:
                {
                    if (fileNameTextBox.Text.Length < 4)
                    {
                        fileNameTextBox.Text += "1";
                    }
                    else
                    {
                        fileNameTextBox.Text = "";
                        fileNameTextBox.Text += "1";
                    }
                    break;
                }
                case Key.NumPad2:
                {
                    if (fileNameTextBox.Text.Length < 4)
                    {
                        fileNameTextBox.Text += "2";
                    }
                    else
                    {
                        fileNameTextBox.Text = "";
                        fileNameTextBox.Text += "2";
                    }
                    break;
                }
                case Key.NumPad3:
                {
                    if (fileNameTextBox.Text.Length < 4)
                    {
                        fileNameTextBox.Text += "3";
                    }
                    else
                    {
                        fileNameTextBox.Text = "";
                        fileNameTextBox.Text += "3";
                    }
                    break;
                }
                case Key.NumPad4:
                {
                    if (fileNameTextBox.Text.Length < 4)
                    {
                        fileNameTextBox.Text += "4";
                    }
                    else
                    {
                        fileNameTextBox.Text = "";
                        fileNameTextBox.Text += "4";
                    }
                    break;
                }
                case Key.NumPad5:
                {
                    if (fileNameTextBox.Text.Length < 4)
                    {
                        fileNameTextBox.Text += "5";
                    }
                    else
                    {
                        fileNameTextBox.Text = "";
                        fileNameTextBox.Text += "5";
                    }
                    break;
                }
                case Key.NumPad6:
                {
                    if (fileNameTextBox.Text.Length < 4)
                    {
                        fileNameTextBox.Text += "6";
                    }
                    else
                    {
                        fileNameTextBox.Text = "";
                        fileNameTextBox.Text += "6";
                    }
                    break;
                }
                case Key.NumPad7:
                {
                    if (fileNameTextBox.Text.Length < 4)
                    {
                        fileNameTextBox.Text += "7";
                    }
                    else
                    {
                        fileNameTextBox.Text = "";
                        fileNameTextBox.Text += "7";
                    }
                    break;
                }
                case Key.NumPad8:
                {
                    if (fileNameTextBox.Text.Length < 4)
                    {
                        fileNameTextBox.Text += "8";
                    }
                    else
                    {
                        fileNameTextBox.Text = "";
                        fileNameTextBox.Text += "8";
                    }
                    break;
                }
                case Key.NumPad9:
                {
                    if (fileNameTextBox.Text.Length < 4)
                    {
                        fileNameTextBox.Text += "9";
                    }
                    else
                    {
                        fileNameTextBox.Text = "";
                        fileNameTextBox.Text += "9";
                    }
                    break;
                }
                case Key.Delete:
                {
                    fileNameTextBox.Text = "";
                    break;
                }
                case Key.Enter:
                {
                    eButton_Click(null, null);
                    break;
                }  
            } 
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void eButton_Click(object sender, RoutedEventArgs e)
        {
            if (fileNameTextBox.Text.Trim().Length == 0)
            {
                System.Windows.MessageBox.Show("Пустое имя песни");
                return;
            }
            if (!SongSaved)
            {
                if (System.Windows.MessageBox.Show("Редактируемая песня еще не сохранена. Сохранить?", "Сервисный режим", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    saveFileButton_Click(null, null);
            }            
            fileNameTextBox.Text = fileNameTextBox.Text.PadLeft(4, '0');
            song = new SongDocument(fileNameTextBox.Text, ScreenWidth);
            this.songGrid.DataContext = sh.docViewer.Document;
            if (song.ServiseMode)
            {
                SongSaved = false;
                servicePanel.Visibility = System.Windows.Visibility.Visible;
                if (System.Windows.MessageBox.Show("Открываемая песня содержит припев?", "Сервисный режим", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    song.InsertRefrain();
                }
            }
            else
            {
                SongSaved = true;
                if (!config.AlwaysServiceMode)
                servicePanel.Visibility = System.Windows.Visibility.Hidden;
            }
            this.previewViewer.Document = song.NextBlock();            
            currentCoopletLabel.Content = Convert.ToString(song.CurrentBlockNumber);
            coopletsCountLabel.Content = Convert.ToString(song.BlocksCount);
        }

        private void HideDocument_Click(object sender, RoutedEventArgs e)
        {
            sh.docViewer.Document = SongDocument.cleanDocument();
            sh.docViewer.Document.FontSize = 1;
            this.songGrid.DataContext = null;
        }

        private void PrevCoopletButton_Click(object sender, RoutedEventArgs e)
        {
            if (song == null)
            {
                this.songGrid.DataContext = null;
                return;
            }
            this.previewViewer.Document = song.PreviousBlock();
            currentCoopletLabel.Content = Convert.ToString(song.CurrentBlockNumber);
            coopletsCountLabel.Content = Convert.ToString(song.BlocksCount);            
        }

        private void NextCoopletButton_Click(object sender, RoutedEventArgs e)
        {
            if (song == null) return;
            this.previewViewer.Document = song.NextBlock();
            currentCoopletLabel.Content = Convert.ToString(song.CurrentBlockNumber);
            coopletsCountLabel.Content = Convert.ToString(song.BlocksCount);
        }

        private void Grid_MouseLeftButtonDown_1(object sender, MouseButtonEventArgs e)
        {
            
        }

        private void Window_MouseLeftButtonDown_1(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void PrevCoopletToScreenButton_Click(object sender, RoutedEventArgs e)
        {
            PrevCoopletButton_Click(null, null);
            ShowButton_Click_1(null, null);            
        }

        private void NextCoopletToScreenButton_Click(object sender, RoutedEventArgs e)
        {
            if (song == null)
            {
                this.songGrid.DataContext = null;
                return;
            }
            if (sh.docViewer.Document.FontSize != 1)
            {                
                NextCoopletButton_Click(null, null);
                ShowButton_Click_1(null, null);            
            }
            else
            {
                ShowButton_Click_1(null, null);
            }            
        }

        private void increaseFontButton_Click(object sender, RoutedEventArgs e)
        {
            if (sh.docViewer.Document.Blocks.Count != 0)
            {
                foreach (Block b in sh.docViewer.Document.Blocks)
                {
                    if(b.FontSize < 1000)
                        b.FontSize += config.FontSizeStep;
                    if (song != null)
                    {
                        song.BlockFontSize = Convert.ToInt32(b.FontSize);
                    }
                }
            }
        }

        private void decreaseFontButton_Click(object sender, RoutedEventArgs e)
        {
            if (sh.docViewer.Document.Blocks.Count != 0)
            {
                foreach (Block b in sh.docViewer.Document.Blocks)
                {
                    if (b.FontSize > config.FontSizeStep)
                        b.FontSize -= config.FontSizeStep;
                    if (song != null)
                    {
                        song.BlockFontSize = Convert.ToInt32(b.FontSize);
                    }
                }
            }
        }

        private void saveFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (song == null) return;
            if (config.SaveAsk)
                if (System.Windows.MessageBox.Show("Уверены, что хотите перезаписать файл?", "Сохранение", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                    return;
            if (!song.SaveSong()) System.Windows.MessageBox.Show("Error");
            else SongSaved = true;
        }

        private void calculateFontButton_Click(object sender, RoutedEventArgs e)
        {
            if (song == null) return;
            if (sh.docViewer.Document.Blocks.Count != 0)
            {      
                foreach (Block b in sh.docViewer.Document.Blocks)
                {
                    song.BlockFontSize = song.CalculateFont();
                    b.FontSize = song.BlockFontSize;
                }
            }
        }

        private void fileNameTextBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (servicePanel.Visibility == System.Windows.Visibility.Hidden)
                servicePanel.Visibility = System.Windows.Visibility.Visible;
            else
                servicePanel.Visibility = System.Windows.Visibility.Hidden;
        }

        private void showExample_Click(object sender, RoutedEventArgs e)
        {
            ListViewExample lve = new ListViewExample();
            lve.ShowDialog();
        }

        private void searchButton_Click(object sender, RoutedEventArgs e)
        {
                        
        }

        private void listView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (listView.SelectedItem == null) return;
            SearchItem sss = (SearchItem)listView.SelectedItem;
            fileNameTextBox.Text = sss.SongName;
            eButton_Click(null, null);
        }

        private void searchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            listView.Items.Clear();
            if (searchTextBox.Text.Trim() == "") return;
            foundSong = new FoundSong();
            foreach (SearchItem sss in foundSong.GetSongFileName(searchTextBox.Text))
            {
                //string result = sss.SongName;
                this.listView.Items.Add(sss);
                //if (result.Trim() != "")
                //{
                //    fileNameTextBox.Text = result;
                //    eButton_Click(null, null);
                //}
            }
        }

        private void backgroundListView_MouseDown(object sender, MouseButtonEventArgs e)
        {
            
        }

        private void backgroundListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (backgroundListView.SelectedItem == null) return;
            PicturesFileName sss = (PicturesFileName)backgroundListView.SelectedItem;
            if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\" + sss.FileName)) return;
            BitmapImage bi3 = new BitmapImage();
            bi3.BeginInit();
            bi3.UriSource = new Uri(AppDomain.CurrentDomain.BaseDirectory + "\\" + sss.FileName, UriKind.Relative);
            bi3.CacheOption = BitmapCacheOption.OnLoad;
            bi3.EndInit();
            if(config.StrechFill == 0)
                backgroundImage.Stretch = Stretch.Fill;
            else if(config.StrechFill == 1)
                backgroundImage.Stretch = Stretch.Uniform;
            else if(config.StrechFill == 2)
                backgroundImage.Stretch = Stretch.UniformToFill;
            backgroundImage.Source = bi3;
        }
                
        private void showBGButton_Click(object sender, RoutedEventArgs e)
        {
            if (backgroundListView.SelectedItem == null) return;
            HideDocument_Click(null, null);
            PicturesFileName sss = (PicturesFileName)backgroundListView.SelectedItem;
            if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\" + sss.FileName)) return;
            ImageBrush myBrush = new ImageBrush();
            Image image = new Image();
            image.Source = new BitmapImage(new Uri(AppDomain.CurrentDomain.BaseDirectory + "\\" + sss.FileName, UriKind.Relative));
            myBrush.ImageSource = image.Source;
            if (config.StrechFill == 0)
                myBrush.Stretch = Stretch.Fill;
            else if (config.StrechFill == 1)
                myBrush.Stretch = Stretch.Uniform;
            else if (config.StrechFill == 2)
                myBrush.Stretch = Stretch.UniformToFill;
            sh.mainScreen.Background = myBrush;
        }

        private void hideBGButton_Click(object sender, RoutedEventArgs e)
        {
            sh.mainScreen.Background = null;
        }
                       

        /*private void Window_MouseDoubleClick_1(object sender, MouseButtonEventArgs e)
        {            
            if(sender.GetType() == typeof(System.Windows.Controls.Button)) return;
            if (servicePanel.Visibility == System.Windows.Visibility.Hidden)
                servicePanel.Visibility = System.Windows.Visibility.Visible;
            else
                servicePanel.Visibility = System.Windows.Visibility.Hidden;
        } */

        /*private void searchButton_Click_1(object sender, RoutedEventArgs e)
        {
            if (searchTextBox.Text.Trim() == "") return;
            foundSong = new FoundSong();
            string result = foundSong.GetSongFileName(searchTextBox.Text);
            if (result.Trim() != "")
            {
                fileNameTextBox.Text = result;
                eButton_Click(null, null);
            }
            else
            {
                System.Windows.MessageBox.Show("Ничего не найдено");
                return;
            }
        }

        private void searchTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            searchButton.IsDefault = true;
        }*/              
    }
}
