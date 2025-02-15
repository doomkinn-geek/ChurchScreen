using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;
using System.Threading;

namespace ChurchScreen
{
    public partial class MainWindow : Window
    {
        public SongDocument song;
        public FoundSong foundSong;
        public Configuration config;
        public ShowScreen sh;
        public ListViewExample lve;
        public int ScreenWidth;

        // Флаг, указывающий, что только что загрузилась новая песня (чтобы первый NextBlock сразу показывал корректно)
        public bool IsNewSongLoaded { get; set; } = false;

        // Флаг «сохранена ли песня перед выходом»
        public bool SongSaved = true;

        public MainWindow()
        {
            InitializeComponent();
            try
            {
                if (File.Exists(Environment.CurrentDirectory + "\\settings.xml"))
                {
                    var reader = new XmlSerializer(typeof(Configuration));
                    using (var file = new StreamReader(Environment.CurrentDirectory + "\\settings.xml"))
                    {
                        config = (Configuration)reader.Deserialize(file);
                    }
                }
                else
                {
                    config = new Configuration();
                }
            }
            catch
            {
                // При ошибках парсинга/чтения используем конфигурацию по умолчанию
                config = new Configuration();
            }
        }

        private void Window_Loaded_1(object sender, RoutedEventArgs e)
        {
            Screen[] screens = Screen.AllScreens;
            string[] files = Directory.GetFiles("pictures");

            // Загрузка списка фоновых изображений
            for (int x = 0; x < files.Length; x++)
            {
                backgroundListView.Items.Add(new PicturesFileName(files[x]));
            }
            if (backgroundListView.Items.Count != 0)
                backgroundListView.SelectedIndex = 0;

            sh = new ShowScreen();

            // Настраиваем окно для второго монитора, если их больше одного
            if (!config.UseOneMonitor && screens.Length > 1)
            {
                sh.Left = screens[1].WorkingArea.X;
                sh.Top = screens[1].WorkingArea.Y;
                sh.Width = screens[1].Bounds.Width;
                sh.Height = screens[1].Bounds.Height;
                sh.docViewer.Document.ColumnWidth = sh.Width + 150;
                ScreenWidth = screens[1].Bounds.Width;
            }
            else
            {
                // Если один монитор или в настройках стоит флаг UseOneMonitor
                sh.Left = screens[0].WorkingArea.X;
                sh.Top = screens[0].WorkingArea.Y;
                sh.Width = screens[0].Bounds.Width;
                sh.Height = screens[0].Bounds.Height;
                sh.docViewer.Document.ColumnWidth = sh.Width + 150;
                ScreenWidth = screens[0].Bounds.Width;
            }

            // Панель сервиса
            if (config.AlwaysServiceMode)
                servicePanel.Visibility = Visibility.Visible;
            else
                servicePanel.Visibility = Visibility.Hidden;

            // Фон по умолчанию
            string filePic = "pictures\\default.jpg";
            bool noDefaultFile = false;

            if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\" + filePic))
            {
                filePic = "pictures\\default.png";
            }
            if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\" + filePic))
            {
                noDefaultFile = true;
            }

            if (!noDefaultFile)
            {
                ImageBrush myBrush = new ImageBrush();
                myBrush.ImageSource = new BitmapImage(new Uri(AppDomain.CurrentDomain.BaseDirectory + "\\" + filePic, UriKind.Relative));
                if (config.StrechFill == 0)
                    myBrush.Stretch = Stretch.Fill;
                else if (config.StrechFill == 1)
                    myBrush.Stretch = Stretch.Uniform;
                else
                    myBrush.Stretch = Stretch.UniformToFill;

                sh.mainScreen.Background = myBrush;
            }

            sh.Show();
            this.Activate();
        }

        private void Window_Closed_1(object sender, EventArgs e)
        {
            // Предупреждение о несохранённой песне
            if (!SongSaved && config.SaveAsk)
            {
                if (System.Windows.MessageBox.Show("Редактируемая песня еще не сохранена. Сохранить?",
                    "Сервисный режим", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    saveFileButton_Click(null, null);
                }
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
            // Убираем фоновое изображение
            sh.mainScreen.Background = null;

            // Сбрасываем флаг, если была только что загружена песня
            if (IsNewSongLoaded)
            {
                IsNewSongLoaded = false;
            }

            // Отображаем выбранный блок на «большом» экране
            sh.docViewer.Document = song.ToMainScreen();

            if (song.IsEnd)
            {
                // Если мы вышли за пределы блоков, скрываем документ и сбрасываем DataContext
                HideDocument_Click(null, null);
                this.songGrid.DataContext = null;
            }
            else
            {
                // В качестве DataContext можно привязать параграф
                if (sh.docViewer.Document != null)
                {
                    foreach (var bl in sh.docViewer.Document.Blocks)
                    {
                        this.songGrid.DataContext = bl;
                    }
                }
            }
        }

        private void HideDocument_Click(object sender, RoutedEventArgs e)
        {
            sh.docViewer.Document = SongDocument.CleanDocument();
            sh.docViewer.Document.FontSize = 1;
            this.songGrid.DataContext = null;
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #region Ввод номера песни с клавиатуры/цифровых кнопок

        private void eButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(fileNameTextBox.Text))
            {
                System.Windows.MessageBox.Show("Пустое имя песни");
                return;
            }

            // Сохраняем ли предыдущую?
            if (!SongSaved && config.SaveAsk)
            {
                if (System.Windows.MessageBox.Show("Редактируемая песня еще не сохранена. Сохранить?",
                    "Сервисный режим", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    saveFileButton_Click(null, null);
                }
            }

            // Дополняем ведущими нулями (до 4 знаков, как было по логике)
            fileNameTextBox.Text = fileNameTextBox.Text.PadLeft(4, '0');

            // Создаём новый SongDocument
            song = new SongDocument(fileNameTextBox.Text, ScreenWidth, config.FontSizeForSplit);

            // Привязка для отладки/просмотра (опционально)
            this.songGrid.DataContext = sh.docViewer.Document;

            if (song.ServiceMode)
            {
                SongSaved = false;
                servicePanel.Visibility = Visibility.Visible;

                // Проверка, нужен ли припев
                if (System.Windows.MessageBox.Show("Открываемая песня содержит припев?",
                    "Сервисный режим", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    song.InsertRefrain();
                }
            }
            else
            {
                SongSaved = true;
                if (!config.AlwaysServiceMode)
                    servicePanel.Visibility = Visibility.Hidden;
            }

            // Показываем в previewViewer первый блок
            this.previewViewer.Document = song.FirstBlock();
            currentCoopletLabel.Content = song.CurrentBlockNumber.ToString();
            coopletsCountLabel.Content = song.BlocksCount.ToString();

            IsNewSongLoaded = true;
            UpdatePreviewFontSize();
        }

        // Цифровые кнопки (0-9). Здесь логика одинаковая – добавляем цифру, если < 4 символов
        private void _0Button_Click(object sender, RoutedEventArgs e)
        {
            AddDigit("0");
        }
        private void _1Button_Click(object sender, RoutedEventArgs e)
        {
            AddDigit("1");
        }
        private void _2Button_Click(object sender, RoutedEventArgs e)
        {
            AddDigit("2");
        }
        private void _3Button_Click(object sender, RoutedEventArgs e)
        {
            AddDigit("3");
        }
        private void _4Button_Click(object sender, RoutedEventArgs e)
        {
            AddDigit("4");
        }
        private void _5Button_Click(object sender, RoutedEventArgs e)
        {
            AddDigit("5");
        }
        private void _6Button_Click(object sender, RoutedEventArgs e)
        {
            AddDigit("6");
        }
        private void _7Button_Click(object sender, RoutedEventArgs e)
        {
            AddDigit("7");
        }
        private void _8Button_Click(object sender, RoutedEventArgs e)
        {
            AddDigit("8");
        }
        private void _9Button_Click(object sender, RoutedEventArgs e)
        {
            AddDigit("9");
        }

        private void cButton_Click(object sender, RoutedEventArgs e)
        {
            fileNameTextBox.Text = "";
        }

        private void AddDigit(string digit)
        {
            if (fileNameTextBox.Text.Length < 4)
            {
                fileNameTextBox.Text += digit;
            }
            else
            {
                fileNameTextBox.Text = digit;
            }
        }

        private void Window_KeyDown_1(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Обработка NumPad
            if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
            {
                string num = (e.Key - Key.NumPad0).ToString();
                AddDigit(num);
            }
            else if (e.Key == Key.Delete)
            {
                fileNameTextBox.Text = "";
            }
            else if (e.Key == Key.Enter)
            {
                eButton_Click(null, null);
            }

            // Ctrl+P – показать/скрыть сервисную панель
            if (e.Key == Key.P && Keyboard.Modifiers == ModifierKeys.Control)
            {
                ToggleServicePanelVisibility();
            }
        }

        private void fileNameTextBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ToggleServicePanelVisibility();
        }

        private void ToggleServicePanelVisibility()
        {
            if (servicePanel.Visibility == Visibility.Visible)
                servicePanel.Visibility = Visibility.Hidden;
            else
                servicePanel.Visibility = Visibility.Visible;
        }

        #endregion

        #region Переключение куплетов (prev/next) и их вывод

        private void PrevCoopletButton_Click(object sender, RoutedEventArgs e)
        {
            if (song == null) return;
            this.previewViewer.Document = song.PreviousBlock();
            currentCoopletLabel.Content = song.CurrentBlockNumber.ToString();
            coopletsCountLabel.Content = song.BlocksCount.ToString();
        }

        private void NextCoopletButton_Click(object sender, RoutedEventArgs e)
        {
            if (song == null) return;
            this.previewViewer.Document = song.NextBlock();
            currentCoopletLabel.Content = song.CurrentBlockNumber.ToString();
            coopletsCountLabel.Content = song.BlocksCount.ToString();
        }

        private void PrevCoopletToScreenButton_Click(object sender, RoutedEventArgs e)
        {
            PrevCoopletButton_Click(null, null);
            ShowButton_Click_1(null, null);
        }

        private void NextCoopletToScreenButton_Click(object sender, RoutedEventArgs e)
        {
            if (song == null) return;

            // Если только что загрузили песню, сначала показываем первый блок
            if (IsNewSongLoaded)
            {
                ShowButton_Click_1(null, null);
                IsNewSongLoaded = false;
            }
            else
            {
                // Если документ не скрыт
                if (sh.docViewer.Document.FontSize != 1)
                {
                    NextCoopletButton_Click(null, null);
                    ShowButton_Click_1(null, null);
                }
                else
                {
                    // Просто показываем текущий блок
                    ShowButton_Click_1(null, null);
                }
            }
        }

        #endregion

        #region Изменение размера шрифта, авторасчёт и сохранение

        private void increaseFontButton_Click(object sender, RoutedEventArgs e)
        {
            if (sh.docViewer.Document?.Blocks == null) return;

            foreach (Block b in sh.docViewer.Document.Blocks)
            {
                if (b.FontSize < 1000)
                {
                    b.FontSize += config.FontSizeStep;
                    if (song != null && song.BlocksCount > 0)
                    {
                        song.BlockFontSize = (int)b.FontSize;
                    }
                }
            }
            UpdatePreviewFontSize();
        }

        private void decreaseFontButton_Click(object sender, RoutedEventArgs e)
        {
            if (sh.docViewer.Document?.Blocks == null) return;

            foreach (Block b in sh.docViewer.Document.Blocks)
            {
                if (b.FontSize > config.FontSizeStep)
                {
                    b.FontSize -= config.FontSizeStep;
                    if (song != null && song.BlocksCount > 0)
                    {
                        song.BlockFontSize = (int)b.FontSize;
                    }
                }
            }
            UpdatePreviewFontSize();
        }

        private void calculateFontButton_Click(object sender, RoutedEventArgs e)
        {
            if (song == null) return;

            if (sh.docViewer.Document?.Blocks.Count != 0)
            {
                int calcSize = song.CalculateFont();
                foreach (Block b in sh.docViewer.Document.Blocks)
                {
                    b.FontSize = calcSize;
                }
                song.BlockFontSize = calcSize;
            }
            UpdatePreviewFontSize();
        }

        private void saveFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (song == null) return;
            if (config.SaveAsk)
            {
                if (System.Windows.MessageBox.Show("Уверены, что хотите перезаписать файл?", "Сохранение", MessageBoxButton.YesNo)
                    != MessageBoxResult.Yes)
                {
                    return;
                }
            }
            if (!song.SaveSong())
                System.Windows.MessageBox.Show("Ошибка при сохранении файла!");
            else
                SongSaved = true;
        }

        /// <summary>
        /// Обновляем размер шрифта в превью, опираясь на фактический размер шрифта на экране.
        /// </summary>
        private void UpdatePreviewFontSize()
        {
            if (song == null || previewViewer == null || song.BlocksCount == 0) return;

            // Берём текущий блок
            FlowDocument doc = previewViewer.Document;
            if (doc == null || doc.Blocks == null) return;

            int previewSize = song.CalculatePreviewFontSize(song.Blocks[song.CurrentBlockNumber - 1]);
            foreach (var block in doc.Blocks)
            {
                block.FontSize = previewSize;
            }
        }

        #endregion

        #region Поиск песен и отображение результатов

        private void searchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            listView.Items.Clear();
            if (string.IsNullOrWhiteSpace(searchTextBox.Text)) return;

            foundSong = new FoundSong();
            foreach (SearchItem si in foundSong.GetSongFileName(searchTextBox.Text))
            {
                listView.Items.Add(si);
            }
        }

        private void listView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (listView.SelectedItem == null) return;
            SearchItem sss = (SearchItem)listView.SelectedItem;
            fileNameTextBox.Text = sss.SongName;
            eButton_Click(null, null);
        }

        private void searchButton_Click(object sender, RoutedEventArgs e)
        {
            // Поиск по нажатию кнопки (при необходимости)
        }

        private void showExample_Click(object sender, RoutedEventArgs e)
        {
            ListViewExample lve = new ListViewExample();
            lve.ShowDialog();
        }

        #endregion

        #region Работа с фоном (картинки)

        private void backgroundListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (backgroundListView.SelectedItem == null) return;
            PicturesFileName pf = (PicturesFileName)backgroundListView.SelectedItem;
            if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\" + pf.FileName)) return;

            BitmapImage bi3 = new BitmapImage();
            bi3.BeginInit();
            bi3.UriSource = new Uri(AppDomain.CurrentDomain.BaseDirectory + "\\" + pf.FileName, UriKind.Relative);
            bi3.CacheOption = BitmapCacheOption.OnLoad;
            bi3.EndInit();

            if (config.StrechFill == 0)
                backgroundImage.Stretch = Stretch.Fill;
            else if (config.StrechFill == 1)
                backgroundImage.Stretch = Stretch.Uniform;
            else
                backgroundImage.Stretch = Stretch.UniformToFill;

            backgroundImage.Source = bi3;
        }

        private void showBGButton_Click(object sender, RoutedEventArgs e)
        {
            if (backgroundListView.SelectedItem == null) return;
            HideDocument_Click(null, null);

            PicturesFileName pf = (PicturesFileName)backgroundListView.SelectedItem;
            if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\" + pf.FileName)) return;

            ImageBrush myBrush = new ImageBrush();
            myBrush.ImageSource = new BitmapImage(new Uri(AppDomain.CurrentDomain.BaseDirectory + "\\" + pf.FileName, UriKind.Relative));

            if (config.StrechFill == 0)
                myBrush.Stretch = Stretch.Fill;
            else if (config.StrechFill == 1)
                myBrush.Stretch = Stretch.Uniform;
            else
                myBrush.Stretch = Stretch.UniformToFill;

            sh.mainScreen.Background = myBrush;
        }

        private void hideBGButton_Click(object sender, RoutedEventArgs e)
        {
            sh.mainScreen.Background = null;
        }

        #endregion

        #region Разбивка/объединение блоков

        private void splitBlocks_Click(object sender, RoutedEventArgs e)
        {
            if (song == null) return;

            // Разбиваем большие блоки
            song.SplitLargeBlocksIfNeeded();

            // Обновляем docViewer и previewViewer
            sh.docViewer.Document = song.ToMainScreen();
            previewViewer.Document = song.FirstBlock();

            currentCoopletLabel.Content = song.CurrentBlockNumber;
            coopletsCountLabel.Content = song.BlocksCount;
            if (song.CurrentBlockNumber != 1)
                IsNewSongLoaded = true;

            UpdatePreviewFontSize();
        }

        private void undoSplitBlocks_Click(object sender, RoutedEventArgs e)
        {
            if (song == null) return;

            // Отменяем разбивку
            song.UndoSplitBlocks();

            // Обновляем docViewer и previewViewer
            sh.docViewer.Document = song.ToMainScreen();
            previewViewer.Document = song.FirstBlock();

            currentCoopletLabel.Content = song.CurrentBlockNumber;
            coopletsCountLabel.Content = song.BlocksCount;
            if (song.CurrentBlockNumber != 1)
                IsNewSongLoaded = true;

            UpdatePreviewFontSize();
        }

        private void undoCurrentSplitButton_Click(object sender, RoutedEventArgs e)
        {
            if (song == null) return;

            // Отменяем разбивку только для текущего блока
            song.UndoSplitForBlock(song.CurrentBlockNumber);

            // Обновляем docViewer и previewViewer
            sh.docViewer.Document = song.ToMainScreen();
            previewViewer.Document = song.CurrentBlock();

            currentCoopletLabel.Content = song.CurrentBlockNumber;
            coopletsCountLabel.Content = song.BlocksCount;
            if (song.CurrentBlockNumber != 1)
                IsNewSongLoaded = true;

            // Можем сразу пересчитать шрифт
            calculateFontButton_Click(null, null);
        }

        #endregion

        private void Window_MouseLeftButtonDown_1(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }
    }
}
