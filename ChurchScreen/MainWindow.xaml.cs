using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using System.Windows.Forms; // для Screen
using System.Windows.Media.Imaging;
using System.Text;

namespace ChurchScreen
{
    public partial class MainWindow : Window
    {
        public SongDocument song;
        public FoundSong foundSong;
        public Configuration config;
        public ShowScreen sh;
        public ListViewExample lve;

        // Храним «физические» размеры монитора, на котором будет ShowScreen.
        private int _monitorWidth;
        private int _monitorHeight;

        public bool IsNewSongLoaded { get; set; } = false;
        public bool SongSaved = true;

        public MainWindow()
        {
            InitializeComponent();
            previewViewer.PreviewMouseWheel += (s, e) => e.Handled = true;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            try
            {
                string settingsPath = System.IO.Path.Combine(Environment.CurrentDirectory, "settings.xml");
                if (File.Exists(settingsPath))
                {
                    var reader = new XmlSerializer(typeof(Configuration));
                    using (var file = new StreamReader(settingsPath))
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
                config = new Configuration();
            }
        }

        private void Window_Loaded_1(object sender, RoutedEventArgs e)
        {
            // Определим мониторы
            Screen[] screens = Screen.AllScreens;
            // По умолчанию берём первый
            Screen targetScreen = screens[0];

            // Если есть два монитора и UseOneMonitor = false, берём второй
            if (!config.UseOneMonitor && screens.Length > 1)
            {
                targetScreen = screens[1];
            }

            // Запоминаем размеры выбранного монитора (физические пиксели)
            _monitorWidth = targetScreen.Bounds.Width;
            _monitorHeight = targetScreen.Bounds.Height;

            // Создаём окно ShowScreen (второй монитор)
            sh = new ShowScreen();

            // Располагаем его на нужном мониторе, на всю область
            sh.Left = targetScreen.WorkingArea.X;
            sh.Top = targetScreen.WorkingArea.Y;
            sh.Width = targetScreen.Bounds.Width;
            sh.Height = targetScreen.Bounds.Height;

            // Чтобы FlowDocumentScrollViewer не делил на колонки:
            sh.docViewer.Document.ColumnWidth = sh.Width + 150;

            try
            {
                // Загружаем список фоновых изображений
                string[] files = Directory.GetFiles("pictures");
                for (int x = 0; x < files.Length; x++)
                {
                    backgroundListView.Items.Add(new PicturesFileName(files[x]));
                }
            }
            catch(Exception ex) 
            {
                ;
            }
            if (backgroundListView.Items.Count != 0)
                backgroundListView.SelectedIndex = 0;

            // Прячем/показываем панель сервиса
            if (config.AlwaysServiceMode)
                servicePanel.Visibility = Visibility.Visible;
            else
                servicePanel.Visibility = Visibility.Hidden;

            // Установим фон по умолчанию (если есть)
            SetDefaultBackground();

            // Показываем окно для второго монитора
            sh.Show();
            this.Activate();
        }

        private void SetDefaultBackground()
        {
            string filePic = "pictures\\default.jpg";
            bool noDefaultFile = false;

            string fullDefaultPath = System.IO.Path.Combine(
                System.AppDomain.CurrentDomain.BaseDirectory,
                filePic
            );

            if (!File.Exists(fullDefaultPath))
            {
                // Пробуем default.png
                filePic = "pictures\\default.png";
                fullDefaultPath = System.IO.Path.Combine(
                    System.AppDomain.CurrentDomain.BaseDirectory,
                    filePic
                );
            }
            if (!File.Exists(fullDefaultPath))
            {
                noDefaultFile = true;
            }

            if (!noDefaultFile)
            {
                ImageBrush myBrush = new ImageBrush();
                myBrush.ImageSource = new BitmapImage(new Uri(fullDefaultPath, UriKind.Absolute));

                if (config.StrechFill == 0)
                    myBrush.Stretch = Stretch.Fill;
                else if (config.StrechFill == 1)
                    myBrush.Stretch = Stretch.Uniform;
                else
                    myBrush.Stretch = Stretch.UniformToFill;

                sh.mainScreen.Background = myBrush;
            }
        }

        private void Window_Closed_1(object sender, EventArgs e)
        {
            // Предупреждение о несохранённой песне
            if (!SongSaved && config.SaveAsk)
            {
                if (System.Windows.MessageBox.Show(
                    "Редактируемая песня еще не сохранена. Сохранить?",
                    "Сервисный режим",
                    MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    saveFileButton_Click(null, null);
                }
            }
            if (sh != null && sh.IsLoaded)
                sh.Close();
        }

        #region Показать на втором мониторе
        private void ShowButton_Click_1(object sender, RoutedEventArgs e)
        {
            if (song == null)
            {
                eButton_Click(null, null);
                return;
            }
            // Если нужно, убираем фоновое изображение
            sh.mainScreen.Background = null;

            if (IsNewSongLoaded)
                IsNewSongLoaded = false;

            // Перед выводом на экран — «автоматически» подберём размер шрифта
            AutoCalculateFontForCurrentBlock();

            // Отображаем текущий блок на втором экране
            sh.docViewer.Document = song.ToMainScreen();

            if (song.IsEnd)
            {
                HideDocument_Click(null, null);
                this.songGrid.DataContext = null;
            }
            else
            {
                // Просто для привязки к интерфейсу
                if (sh.docViewer.Document != null)
                {
                    foreach (var bl in sh.docViewer.Document.Blocks)
                    {
                        this.songGrid.DataContext = bl;
                    }
                }
            }
        }
        #endregion

        #region Автоподбор шрифта (старый подход)
        /// <summary>
        /// Пересчитываем размер шрифта для текущего блока,
        /// используя старый метод CalculateFont() в SongDocument.
        /// </summary>
        private void AutoCalculateFontForCurrentBlock()
        {
            if (song == null || song.BlocksCount == 0) return;

            // Старый метод: CalculateFont для текущего блока
            int newFont = song.CalculateFont();
            song.BlockFontSize = newFont;
        }
        #endregion

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

        #region Кнопки ввода номера песни

        private void eButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(fileNameTextBox.Text))
            {
                System.Windows.MessageBox.Show("Пустое имя песни");
                return;
            }

            // Если текущая песня не сохранена
            if (!SongSaved && config.SaveAsk)
            {
                if (System.Windows.MessageBox.Show("Редактируемая песня еще не сохранена. Сохранить?",
                    "Сервисный режим", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    saveFileButton_Click(null, null);
                }
            }

            // Дополняем до 4 символов (например "0012")
            fileNameTextBox.Text = fileNameTextBox.Text.PadLeft(4, '0');

            // Определяем DPI для окна ShowScreen (или для this, если хотим)
            // Обычно берём PresentationSource.FromVisual(sh), т.к. sh уже показано на втором мониторе.
            var source = PresentationSource.FromVisual(sh);
            if (source != null)
            {
                Matrix m = source.CompositionTarget.TransformToDevice;
                double dpiX = m.M11;
                double dpiY = m.M22;

                double dipWidth = _monitorWidth / dpiX;
                double dipHeight = _monitorHeight / dpiY;

                song = new SongDocument(fileNameTextBox.Text,
                                        (int)dipWidth,
                                        (int)dipHeight,
                                        config.FontSizeForSplit);
            }
            else
            {
                // fallback
                song = new SongDocument(fileNameTextBox.Text,
                                        _monitorWidth,
                                        (int)(_monitorWidth * 9.0 / 16.0),
                                        config.FontSizeForSplit);
            }


            // Если файл "сервисный"
            if (song.ServiceMode)
            {
                SongSaved = false;
                servicePanel.Visibility = Visibility.Visible;

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

            // Переходим к первому блоку
            previewViewer.Document = song.FirstBlock();
            currentCoopletLabel.Content = song.CurrentBlockNumber;
            coopletsCountLabel.Content = song.BlocksCount;

            // «Автоподбор» по формуле SongDocument
            AutoCalculateFontForCurrentBlock();

            IsNewSongLoaded = true;
            UpdatePreviewFontSize();
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

        private void _0Button_Click(object sender, RoutedEventArgs e) => AddDigit("0");
        private void _1Button_Click(object sender, RoutedEventArgs e) => AddDigit("1");
        private void _2Button_Click(object sender, RoutedEventArgs e) => AddDigit("2");
        private void _3Button_Click(object sender, RoutedEventArgs e) => AddDigit("3");
        private void _4Button_Click(object sender, RoutedEventArgs e) => AddDigit("4");
        private void _5Button_Click(object sender, RoutedEventArgs e) => AddDigit("5");
        private void _6Button_Click(object sender, RoutedEventArgs e) => AddDigit("6");
        private void _7Button_Click(object sender, RoutedEventArgs e) => AddDigit("7");
        private void _8Button_Click(object sender, RoutedEventArgs e) => AddDigit("8");
        private void _9Button_Click(object sender, RoutedEventArgs e) => AddDigit("9");

        private void cButton_Click(object sender, RoutedEventArgs e)
        {
            fileNameTextBox.Text = "";
        }

        private void Window_KeyDown_1(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Цифры на NumPad
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

            // Ctrl+P => показать/спрятать сервисную панель
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
            servicePanel.Visibility = servicePanel.Visibility == Visibility.Visible
                ? Visibility.Hidden
                : Visibility.Visible;
        }

        #endregion

        #region Переключение куплетов

        private void PrevCoopletButton_Click(object sender, RoutedEventArgs e)
        {
            if (song == null) return;

            previewViewer.Document = song.PreviousBlock();
            currentCoopletLabel.Content = song.CurrentBlockNumber;
            coopletsCountLabel.Content = song.BlocksCount;

            AutoCalculateFontForCurrentBlock();
            UpdatePreviewFontSize();
        }

        private void NextCoopletButton_Click(object sender, RoutedEventArgs e)
        {
            if (song == null) return;

            previewViewer.Document = song.NextBlock();
            currentCoopletLabel.Content = song.CurrentBlockNumber;
            coopletsCountLabel.Content = song.BlocksCount;

            AutoCalculateFontForCurrentBlock();
            UpdatePreviewFontSize();
        }

        private void PrevCoopletToScreenButton_Click(object sender, RoutedEventArgs e)
        {
            PrevCoopletButton_Click(null, null);
            ShowButton_Click_1(null, null);
        }

        private void NextCoopletToScreenButton_Click(object sender, RoutedEventArgs e)
        {
            if (song == null) return;

            if (IsNewSongLoaded)
            {
                ShowButton_Click_1(null, null);
                IsNewSongLoaded = false;
            }
            else
            {
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
        }

        #endregion

        #region Изменение размера шрифта (кнопки +/-, авто)

        private void increaseFontButton_Click(object sender, RoutedEventArgs e)
        {
            if (sh.docViewer.Document?.Blocks == null) return;

            // Для каждого блока в ShowScreen увеличиваем шрифт
            // (хотя обычно там всего 1 Paragraph, но на всякий случай)
            foreach (Block b in sh.docViewer.Document.Blocks)
            {
                if (b.FontSize < 1000)
                {
                    b.FontSize += config.FontSizeStep;

                    // Сохраняем новое значение в SongDocument
                    if (song != null && song.BlocksCount > 0)
                    {
                        // Запоминаем в объекте SongDocument
                        song.BlockFontSize = (int)b.FontSize;
                    }
                }
            }

            // Обновляем предпросмотр, делая «прямое» масштабирование
            UpdatePreviewFontSizeByRatio();
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

            UpdatePreviewFontSizeByRatio();
        }

        /// <summary>
        /// Шрифт в previewViewer делаем пропорциональным
        /// текущему шрифту в SongDocument.BlockFontSize.
        /// </summary>
        private void UpdatePreviewFontSizeByRatio()
        {
            if (song == null) return;
            if (previewViewer?.Document == null) return;
            if (song.BlocksCount == 0) return;

            // «Основной» шрифт (текущего блока) на ShowScreen
            int mainFontSize = song.BlockFontSize;
            if (mainFontSize < 1) mainFontSize = 1;

            // Рассчитываем коэффициент уменьшения 
            // Допустим, preview шириной 320, а ScreenWidth = song.ScreenWidth
            // (который мы передали в SongDocument при создании)
            double scaleFactor = 320.0 / song.ScreenWidth;
            if (scaleFactor > 1.0) scaleFactor = 1.0; // На случай очень узкого экрана

            double previewSize = mainFontSize * scaleFactor;
            if (previewSize < 8) previewSize = 8;   // минимальный размер шрифта в превью

            foreach (Block block in previewViewer.Document.Blocks)
            {
                block.FontSize = previewSize;
            }
        }



        private void calculateFontButton_Click(object sender, RoutedEventArgs e)
        {
            // Перерасчёт шрифта (старый метод)
            AutoCalculateFontForCurrentBlock();

            // Обновляем документ
            if (song != null)
            {
                sh.docViewer.Document = song.ToMainScreen();
                previewViewer.Document = song.CurrentBlock();
            }
        }

        #endregion

        private void UpdatePreviewFontSize()
        {
            if (song == null) return;
            if (previewViewer?.Document == null) return;
            if (song.BlocksCount == 0) return;

            // Считаем «previewSize»
            int previewSize = song.CalculatePreviewFontSize(song.Blocks[song.CurrentBlockNumber - 1]);
            foreach (Block block in previewViewer.Document.Blocks)
            {
                block.FontSize = previewSize;
            }
        }

        #region Сохранение

        private void saveFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (song == null) return;
            if (config.SaveAsk)
            {
                if (System.Windows.MessageBox.Show(
                    "Уверены, что хотите перезаписать файл?",
                    "Сохранение",
                    MessageBoxButton.YesNo
                ) != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            if (!song.SaveSong())
                System.Windows.MessageBox.Show("Ошибка при сохранении файла!");
            else
                SongSaved = true;
        }

        #endregion

        #region Поиск

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
            // можете реализовать
        }

        private void showExample_Click(object sender, RoutedEventArgs e)
        {
            ListViewExample lve = new ListViewExample();
            lve.ShowDialog();
        }

        #endregion

        #region Фоновые изображения

        private void backgroundListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (backgroundListView.SelectedItem == null) return;
            PicturesFileName pf = (PicturesFileName)backgroundListView.SelectedItem;
            string fullName = System.IO.Path.Combine(
                System.AppDomain.CurrentDomain.BaseDirectory,
                pf.FileName
            );
            if (!File.Exists(fullName)) return;

            BitmapImage bi3 = new BitmapImage();
            bi3.BeginInit();
            bi3.UriSource = new Uri(fullName, UriKind.Absolute);
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
            string fullName = System.IO.Path.Combine(
                System.AppDomain.CurrentDomain.BaseDirectory,
                pf.FileName
            );
            if (!File.Exists(fullName)) return;

            ImageBrush myBrush = new ImageBrush();
            myBrush.ImageSource = new BitmapImage(new Uri(fullName, UriKind.Absolute));

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

        #region Разбивка / отмена разбивки блоков

        private void splitBlocks_Click(object sender, RoutedEventArgs e)
        {
            if (song == null) return;

            song.SplitLargeBlocksIfNeeded();

            // Обновляем вывод
            sh.docViewer.Document = song.ToMainScreen();
            previewViewer.Document = song.FirstBlock();

            currentCoopletLabel.Content = song.CurrentBlockNumber;
            coopletsCountLabel.Content = song.BlocksCount;

            // После разбивки можно заново «автоматически» подобрать шрифт
            AutoCalculateFontForCurrentBlock();
            UpdatePreviewFontSize();
        }

        private void undoSplitBlocks_Click(object sender, RoutedEventArgs e)
        {
            if (song == null) return;
            song.UndoSplitBlocks();

            sh.docViewer.Document = song.ToMainScreen();
            previewViewer.Document = song.FirstBlock();

            currentCoopletLabel.Content = song.CurrentBlockNumber;
            coopletsCountLabel.Content = song.BlocksCount;

            AutoCalculateFontForCurrentBlock();
            UpdatePreviewFontSize();
        }

        private void undoCurrentSplitButton_Click(object sender, RoutedEventArgs e)
        {
            if (song == null) return;

            song.UndoSplitForBlock(song.CurrentBlockNumber);

            sh.docViewer.Document = song.ToMainScreen();
            previewViewer.Document = song.CurrentBlock();

            currentCoopletLabel.Content = song.CurrentBlockNumber;
            coopletsCountLabel.Content = song.BlocksCount;

            AutoCalculateFontForCurrentBlock();
            UpdatePreviewFontSize();
        }

        #endregion

        private void Window_MouseLeftButtonDown_1(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }
    }
}
