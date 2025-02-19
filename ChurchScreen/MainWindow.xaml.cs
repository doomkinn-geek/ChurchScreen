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

        // Размеры «физические» (пиксели) монитора второго экрана
        private int _monitorWidth;
        private int _monitorHeight;

        public bool IsNewSongLoaded { get; set; } = false;
        public bool SongSaved = true;

        public MainWindow()
        {
            InitializeComponent();

            // Запрет прокрутки колёсиком в preview
            previewViewer.PreviewMouseWheel += (s, e) => e.Handled = true;

            // Разрешаем чтение кодировок 1251 и т.д.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // Читаем конфиг
            try
            {
                string settingsPath = Path.Combine(Environment.CurrentDirectory, "settings.xml");
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
            // Определяем мониторы
            Screen[] screens = Screen.AllScreens;
            Screen targetScreen = screens[0]; // по умолчанию

            // Если есть второй — и не установлен UseOneMonitor
            if (!config.UseOneMonitor && screens.Length > 1)
            {
                targetScreen = screens[1];
            }

            // Запоминаем «физический» размер
            _monitorWidth = targetScreen.Bounds.Width;
            _monitorHeight = targetScreen.Bounds.Height;

            // Создаём окно второго монитора
            sh = new ShowScreen
            {
                Left = targetScreen.WorkingArea.X,
                Top = targetScreen.WorkingArea.Y,
                Width = targetScreen.Bounds.Width,
                Height = targetScreen.Bounds.Height
            };

            // FlowDocument не делим на колонки
            sh.docViewer.Document.ColumnWidth = sh.Width + 150;

            // Пробуем найти «картинки» для фона
            try
            {
                string[] files = Directory.GetFiles("pictures");
                for (int x = 0; x < files.Length; x++)
                {
                    backgroundListView.Items.Add(new PicturesFileName(files[x]));
                }
            }
            catch { /* no op */ }

            if (backgroundListView.Items.Count > 0)
                backgroundListView.SelectedIndex = 0;

            // Сервисная панель — вкл/выкл
            servicePanel.Visibility = config.AlwaysServiceMode
                ? Visibility.Visible
                : Visibility.Hidden;

            // Попробуем поставить фон по умолчанию
            SetDefaultBackground();

            // Показать окно
            sh.Show();
            this.Activate();
        }

        private void SetDefaultBackground()
        {
            string filePic = "pictures\\default.jpg";
            bool noDefaultFile = false;

            string fullDefaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filePic);

            if (!File.Exists(fullDefaultPath))
            {
                filePic = "pictures\\default.png";
                fullDefaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filePic);
            }
            if (!File.Exists(fullDefaultPath))
            {
                noDefaultFile = true;
            }

            if (!noDefaultFile)
            {
                var myBrush = new ImageBrush
                {
                    ImageSource = new BitmapImage(new Uri(fullDefaultPath, UriKind.Absolute))
                };
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
            // Если была несохранённая песня
            if (!SongSaved && config.SaveAsk)
            {
                if (System.Windows.MessageBox.Show(
                    "Редактируемая песня еще не сохранена. Сохранить?",
                    "Сервисный режим",
                    MessageBoxButton.YesNo
                ) == MessageBoxResult.Yes)
                {
                    saveFileButton_Click(null, null);
                }
            }

            if (sh != null && sh.IsLoaded)
                sh.Close();
        }

        #region Кнопка "Показать" (второй монитор)

        private void ShowButton_Click_1(object sender, RoutedEventArgs e)
        {
            if (song == null)
            {
                eButton_Click(null, null);
                return;
            }
            // Убираем фон, если хотим
            sh.mainScreen.Background = null;

            if (IsNewSongLoaded)
                IsNewSongLoaded = false;

            // Автоподбор
            AutoCalculateFontForCurrentBlock();

            // Устанавливаем документ
            sh.docViewer.Document = song.ToMainScreen();

            if (song.IsEnd)
            {
                HideDocument_Click(null, null);
                songGrid.DataContext = null;
            }
            else
            {
                // Просто чтобы привязка (DataContext) на что-то смотрела
                if (sh.docViewer.Document != null)
                {
                    foreach (var bl in sh.docViewer.Document.Blocks)
                    {
                        songGrid.DataContext = bl;
                    }
                }
            }
        }

        #endregion

        #region Автоподбор шрифта (старый метод)

        private void AutoCalculateFontForCurrentBlock()
        {
            if (song == null || song.BlocksCount == 0) return;
            int newFont = song.CalculateFont();
            song.BlockFontSize = newFont;
        }

        #endregion

        private void HideDocument_Click(object sender, RoutedEventArgs e)
        {
            sh.docViewer.Document = SongDocument.CleanDocument();
            sh.docViewer.Document.FontSize = 1;
            songGrid.DataContext = null;
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #region Кнопки ввода номера (eButton_Click и т.д.)

        private void eButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(fileNameTextBox.Text))
            {
                System.Windows.MessageBox.Show("Пустое имя песни");
                return;
            }

            // Если есть несохранённая
            if (!SongSaved && config.SaveAsk)
            {
                if (System.Windows.MessageBox.Show(
                    "Редактируемая песня еще не сохранена. Сохранить?",
                    "Сервисный режим",
                    MessageBoxButton.YesNo
                ) == MessageBoxResult.Yes)
                {
                    saveFileButton_Click(null, null);
                }
            }

            // Дополняем до 4 символов
            fileNameTextBox.Text = fileNameTextBox.Text.PadLeft(4, '0');

            // DPI
            var source = PresentationSource.FromVisual(sh);
            if (source != null)
            {
                Matrix m = source.CompositionTarget.TransformToDevice;
                double dpiX = m.M11;
                double dpiY = m.M22;

                double dipWidth = _monitorWidth / dpiX;
                double dipHeight = _monitorHeight / dpiY;

                // Создаём song
                song = new SongDocument(
                    fileNameTextBox.Text,
                    (int)dipWidth,
                    (int)dipHeight,
                    config.FontSizeForSplit
                );
            }
            else
            {
                // fallback
                song = new SongDocument(
                    fileNameTextBox.Text,
                    _monitorWidth,
                    (int)(_monitorWidth * 9.0 / 16.0),
                    config.FontSizeForSplit
                );
            }

            // Если «сервисный» файл
            if (song.ServiceMode)
            {
                SongSaved = false;
                servicePanel.Visibility = Visibility.Visible;

                if (System.Windows.MessageBox.Show(
                    "Открываемая песня содержит припев?",
                    "Сервисный режим",
                    MessageBoxButton.YesNo
                ) == MessageBoxResult.Yes)
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

            // Автоподбор
            AutoCalculateFontForCurrentBlock();

            IsNewSongLoaded = true;
            UpdatePreviewFontSize();
        }

        private void AddDigit(string digit)
        {
            if (fileNameTextBox.Text.Length < 4)
                fileNameTextBox.Text += digit;
            else
                fileNameTextBox.Text = digit;
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
        private void cButton_Click(object sender, RoutedEventArgs e) => fileNameTextBox.Text = "";

        private void Window_KeyDown_1(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // NumPad цифры
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
            servicePanel.Visibility =
                servicePanel.Visibility == Visibility.Visible
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

        #region Изменение размера шрифта (+/-) и обновление предпросмотра

        private void increaseFontButton_Click(object sender, RoutedEventArgs e)
        {
            if (sh.docViewer.Document?.Blocks == null) return;

            // Увеличиваем шрифт на втором экране
            foreach (Block b in sh.docViewer.Document.Blocks)
            {
                if (b.FontSize < 1000)
                {
                    b.FontSize += config.FontSizeStep;
                    if (song != null && song.BlocksCount > 0)
                        song.BlockFontSize = (int)b.FontSize;
                }
            }

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
                        song.BlockFontSize = (int)b.FontSize;
                }
            }

            UpdatePreviewFontSizeByRatio();
        }

        /// <summary>
        /// Уменьшаем шрифт в previewViewer пропорционально текущему 
        /// (song.BlockFontSize) на ShowScreen.
        /// </summary>
        private void UpdatePreviewFontSizeByRatio()
        {
            if (song == null || previewViewer?.Document == null || song.BlocksCount == 0) return;

            int mainFontSize = song.BlockFontSize;
            if (mainFontSize < 1) mainFontSize = 1;

            // Коэффициент (preview шириной 320, делим на реальную ширину DIP)
            double scaleFactor = 320.0 / song.ScreenWidth;
            if (scaleFactor > 1.0) scaleFactor = 1.0; // если экран узкий

            double previewSize = mainFontSize * scaleFactor;
            if (previewSize < 8) previewSize = 8; // минимум 8

            foreach (Block block in previewViewer.Document.Blocks)
            {
                block.FontSize = previewSize;
            }
        }

        private void calculateFontButton_Click(object sender, RoutedEventArgs e)
        {
            // Старый автоподбор
            AutoCalculateFontForCurrentBlock();

            if (song != null)
            {
                // Обновляем второй экран
                sh.docViewer.Document = song.ToMainScreen();
                // Обновляем предпросмотр
                previewViewer.Document = song.CurrentBlock();
                // И делаем масштабирование
                UpdatePreviewFontSizeByRatio();
            }
        }

        #endregion

        #region Старый метод UpdatePreviewFontSize (для совместимости)

        /// <summary>
        /// Раньше вы считали previewSize = song.CalculatePreviewFontSize(...),
        /// теперь вызываем только UpdatePreviewFontSizeByRatio,
        /// чтобы всё было синхронизировано.
        /// </summary>
        private void UpdatePreviewFontSize()
        {
            if (song == null || previewViewer?.Document == null || song.BlocksCount == 0) return;

            // Вместо отдельного рассчёта — сразу делаем ratio:
            UpdatePreviewFontSizeByRatio();
        }

        #endregion

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

            var sss = (SearchItem)listView.SelectedItem;
            fileNameTextBox.Text = sss.SongName;
            eButton_Click(null, null);
        }

        private void searchButton_Click(object sender, RoutedEventArgs e)
        {
            // ...
        }

        private void showExample_Click(object sender, RoutedEventArgs e)
        {
            var lve = new ListViewExample();
            lve.ShowDialog();
        }

        #endregion

        #region Фоновые изображения

        private void backgroundListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (backgroundListView.SelectedItem == null) return;

            var pf = (PicturesFileName)backgroundListView.SelectedItem;
            string fullName = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                pf.FileName
            );
            if (!File.Exists(fullName)) return;

            var bi3 = new BitmapImage();
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

            var pf = (PicturesFileName)backgroundListView.SelectedItem;
            string fullName = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                pf.FileName
            );
            if (!File.Exists(fullName)) return;

            var myBrush = new ImageBrush
            {
                ImageSource = new BitmapImage(new Uri(fullName, UriKind.Absolute))
            };

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

        #region Разбивка / отмена (Split) блоков

        private void splitBlocks_Click(object sender, RoutedEventArgs e)
        {
            if (song == null) return;
            song.SplitLargeBlocksIfNeeded();

            sh.docViewer.Document = song.ToMainScreen();
            previewViewer.Document = song.FirstBlock();

            currentCoopletLabel.Content = song.CurrentBlockNumber;
            coopletsCountLabel.Content = song.BlocksCount;

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
