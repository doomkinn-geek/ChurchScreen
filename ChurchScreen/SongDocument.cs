using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Documents;
using System.Windows.Media;

namespace ChurchScreen
{
    /// <summary>
    /// Класс отвечает за загрузку, хранение и отображение песни,
    /// а также за разбивку/объединение блоков.
    /// </summary>
    public class SongDocument
    {
        private int _currentBlockNumber;            // Текущий блок (1-based индекс)
        public bool ServiceMode { get; private set; }  // Режим сервиса (определяется по содержимому файла)
        public string FileName { get; private set; }   // Полное имя файла
        public List<string> Blocks { get; private set; }      // Список текстовых блоков (куплеты и т.п.)
        private List<int> _blocksFontSizes;         // Список размеров шрифтов для каждого блока
        public int ScreenWidth { get; private set; }    // Ширина «основного» экрана (для расчётов шрифта)
        public int FontSizeForSplit { get; private set; } // Пороговый размер шрифта для разбивки «слишком больших» блоков

        // Возвращает число блоков (куплетов) в песне
        public int BlocksCount => Blocks?.Count ?? 0;

        /// <summary>
        /// Текущий номер блока (1-based). Если выходит за границы, возвращаем минимальное/максимальное
        /// </summary>
        public int CurrentBlockNumber
        {
            get
            {
                if (BlocksCount == 0) return 0;
                return Math.Min(_currentBlockNumber, BlocksCount);
            }
        }

        /// <summary>
        /// Размер шрифта для текущего блока.
        /// Если блоков нет – возвращаем 0.
        /// </summary>
        public int BlockFontSize
        {
            get
            {
                if (CurrentBlockNumber == 0) return 0;
                return _blocksFontSizes[CurrentBlockNumber - 1];
            }
            set
            {
                if (CurrentBlockNumber == 0) return;
                _blocksFontSizes[CurrentBlockNumber - 1] = value;
            }
        }

        /// <summary>
        /// Флаг, означающий, что мы «вышли за последний блок».
        /// Если текущий блок > BlocksCount, значит, дошли до конца (IsEnd = true).
        /// </summary>
        public bool IsEnd => _currentBlockNumber > BlocksCount;

        /// <summary>
        /// Коэффициенты ширины для разных символов (примерная оценка,
        /// чтобы точнее считать длину «широких» и «узких» символов).
        /// </summary>
        private readonly Dictionary<char, double> _widthCoefficients = new Dictionary<char, double>
        {
            // Латинские
            { 'W', 1.5 }, { 'M', 1.4 }, { 'm', 1.3 }, { 'w', 1.3 },
            { 'i', 0.7 }, { 'l', 0.6 }, { 'j', 0.6 }, { 't', 0.8 }, { 'f', 0.8 }, { 'r', 0.9 },
            // Кириллические
            { 'Ш', 1.4 }, { 'М', 1.4 }, { 'м', 1.3 }, { 'ш', 1.3 }, { 'щ', 1.5 }, { 'ф', 1.3 },
            { 'й', 0.8 }, { 'л', 0.9 }, { 'т', 0.9 }, { 'и', 0.9 },
            // Цифры и знаки препинания
            { '1', 0.8 }, { '.', 0.6 }, { ',', 0.6 }, { ':', 0.6 }, { ';', 0.6 }, { '!', 0.7 },
            // При необходимости добавляйте другие символы
        };

        /// <summary>
        /// Конструктор с инициализацией.
        /// </summary>
        /// <param name="fileName">Имя или путь к файлу (без/с .txt)</param>
        /// <param name="screenWidth">Ширина экрана для расчёта размера шрифта</param>
        /// <param name="fontSizeForSplit">Пороговый размер шрифта, при котором считаем блок слишком большим и делим</param>
        public SongDocument(string fileName, int screenWidth, int fontSizeForSplit)
        {
            FontSizeForSplit = fontSizeForSplit;
            ScreenWidth = screenWidth;
            Initialize(fileName);
        }

        /// <summary>
        /// Инициализация: загрузка из файла, определение ServiceMode, парсинг блоков.
        /// </summary>
        private void Initialize(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return;

            // Дополняем .txt при необходимости
            FileName = fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                ? fileName
                : fileName + ".txt";

            // Если файл не найден – пробуем искать в папке songs\
            if (!File.Exists(FileName))
            {
                FileName = Path.Combine(Environment.CurrentDirectory, "songs", Path.GetFileName(FileName));
                if (!File.Exists(FileName)) return;
            }

            _currentBlockNumber = 0;
            Blocks = new List<string>();
            _blocksFontSizes = new List<int>();

            string fileData = ReadFileContent(FileName);
            if (string.IsNullOrEmpty(fileData)) return;

            // Определяем, является ли это «сервисным» файлом (нет специальных тэгов @01)
            ServiceMode = !fileData.Contains("@01");

            if (ServiceMode)
            {
                // Разбиваем по двойным переводам строк
                LoadTextByEmptyLines(fileData);
            }
            else
            {
                // Парсим по шаблону @NN#FFF(текст)$NN
                LoadTextAndSplitIntoBlocks(fileData);
            }

            // Добавим в конец «символы окончания» (*****), если нужно
            AddEndSymbols();
        }

        /// <summary>
        /// Считываем содержимое файла, определяем кодировку автоматически.
        /// </summary>
        private string ReadFileContent(string filePath)
        {
            try
            {
                Encoding encoding = GetFileEncoding(filePath);
                if (encoding == Encoding.UTF8)
                {
                    return File.ReadAllText(filePath, Encoding.UTF8);
                }
                else
                {
                    byte[] ansiBytes = File.ReadAllBytes(filePath);
                    return Encoding.Default.GetString(ansiBytes);
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Определение кодировки файла «по сигнатуре» (BOM).
        /// Если сигнатуры нет – возвращаем Encoding.Default.
        /// </summary>
        public static Encoding GetFileEncoding(string srcFile)
        {
            using (var file = new FileStream(srcFile, FileMode.Open, FileAccess.Read))
            {
                var buffer = new byte[5];
                file.Read(buffer, 0, 5);

                // UTF-8 BOM
                if (buffer[0] == 0xef && buffer[1] == 0xbb && buffer[2] == 0xbf)
                    return Encoding.UTF8;

                // Unicode (Big Endian)
                if (buffer[0] == 0xfe && buffer[1] == 0xff)
                    return Encoding.GetEncoding(1201);

                // UTF-32
                if (buffer[0] == 0 && buffer[1] == 0 && buffer[2] == 0xfe && buffer[3] == 0xff)
                    return Encoding.UTF32;

                // UTF-7 BOM
                if (buffer[0] == 0x2b && buffer[1] == 0x2f && buffer[2] == 0x76)
                    return Encoding.UTF7;

                // UTF-16 Unicode
                if (buffer[0] == 0xFF && buffer[1] == 0xFE)
                    return Encoding.GetEncoding(1200);

                // Если ничего не подходит – Encoding.Default
                return Encoding.Default;
            }
        }

        #region Загрузка и парсинг текстовых блоков

        /// <summary>
        /// Загрузка «обычного» (несервисного) файла: ищем блоки вида @NN#FFF(текст)$NN
        /// </summary>
        private void LoadTextAndSplitIntoBlocks(string fileData)
        {
            // Шаблон: @NN#FFF(текст)...$NN
            // NN – 2 цифры номера (01,02,...), FFF – 3 цифры размера шрифта
            var blockPattern = @"@\d{2}#(\d{3})(.*?)\$\d{2}";
            var matches = Regex.Matches(fileData, blockPattern, RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                var textBlock = match.Groups[2].Value.Trim();      // Текст внутри
                var fontSizeStr = match.Groups[1].Value;           // FFF

                Blocks.Add(textBlock);

                if (int.TryParse(fontSizeStr, out int fontSize))
                    _blocksFontSizes.Add(fontSize);
                else
                    _blocksFontSizes.Add(-1);  // На случай, если не получилось считать
            }

            // Если вдруг не совпадает количество блоков и размеров
            if (Blocks.Count != _blocksFontSizes.Count)
            {
                ResetSongData();
            }
        }

        /// <summary>
        /// Загрузка в сервисном режиме: блоки разделены пустыми строками (два перевода строки).
        /// Каждый «абзац» – это отдельный блок.
        /// </summary>
        private void LoadTextByEmptyLines(string fileData)
        {
            // Разделитель: двойной перевод строки
            var rawBlocks = fileData
                .Split(new string[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(b => b.Trim())
                .ToArray();

            foreach (string rawBlock in rawBlocks)
            {
                string cleanedBlock = RemoveTrailingSpaces(rawBlock);
                // Проверим, нужно ли его разбить (если CalculateFont вернёт маленький размер)
                List<int> splitSizes;
                var splittedBlocks = SplitBlockIfNeeded(cleanedBlock, out splitSizes);

                Blocks.AddRange(splittedBlocks);
                _blocksFontSizes.AddRange(splitSizes);
            }

            if (Blocks.Count != _blocksFontSizes.Count)
            {
                ResetSongData();
            }
        }

        /// <summary>
        /// Сброс данных, если возникли несоответствия.
        /// </summary>
        private void ResetSongData()
        {
            _currentBlockNumber = 0;
            Blocks?.Clear();
            _blocksFontSizes?.Clear();
        }

        #endregion

        #region Методы для доступа к блокам (Next, Prev, First и т.д.)

        /// <summary>
        /// Переход к первому блоку и возврат FlowDocument для превью.
        /// </summary>
        public FlowDocument FirstBlock()
        {
            if (BlocksCount == 0)
                return CreateDocumentWithText("<ПУСТО>");

            _currentBlockNumber = 1;
            return GetDocument(_currentBlockNumber, previewMode: true);
        }

        /// <summary>
        /// Текущий блок (FlowDocument) – для превью.
        /// </summary>
        public FlowDocument CurrentBlock()
        {
            if (BlocksCount == 0)
                return CreateDocumentWithText("<ПУСТО>");

            return GetDocument(CurrentBlockNumber, previewMode: true);
        }

        /// <summary>
        /// Следующий блок (FlowDocument) – для превью.
        /// Если выходим за границы, пишем «<КОНЕЦ>».
        /// </summary>
        public FlowDocument NextBlock()
        {
            if (BlocksCount == 0)
                return CreateDocumentWithText("<ПУСТО>");

            // Если уже на последнем блоке – следующий будет «конец»
            if (_currentBlockNumber >= BlocksCount)
            {
                _currentBlockNumber = BlocksCount + 1;
                return CreateDocumentWithText("<КОНЕЦ>");
            }

            _currentBlockNumber++;
            return GetDocument(_currentBlockNumber, previewMode: true);
        }

        /// <summary>
        /// Предыдущий блок (FlowDocument) – для превью.
        /// Если уже на первом, остаёмся на нём.
        /// </summary>
        public FlowDocument PreviousBlock()
        {
            if (BlocksCount == 0)
                return CreateDocumentWithText("<ПУСТО>");

            if (_currentBlockNumber <= 1)
            {
                _currentBlockNumber = 1;
                return GetDocument(_currentBlockNumber, previewMode: true);
            }

            _currentBlockNumber--;
            return GetDocument(_currentBlockNumber, previewMode: true);
        }

        /// <summary>
        /// Формирует документ для основного экрана (большие буквы и т.д.).
        /// </summary>
        public FlowDocument ToMainScreen()
        {
            if (BlocksCount == 0)
                return CreateDocumentWithText("<ПУСТО>");

            return GetDocument(CurrentBlockNumber, previewMode: false);
        }

        /// <summary>
        /// Статический метод: возвращает «пустой» документ (используется для HideDocument).
        /// </summary>
        public static FlowDocument CleanDocument()
        {
            return new FlowDocument();
        }

        #endregion

        #region Методы сохранения и вычисления шрифтов

        /// <summary>
        /// Сохранение песни в файл (формат @NN#FFF...$NN).
        /// </summary>
        public bool SaveSong()
        {
            if (BlocksCount == 0)
                return false; // Нет блоков – не сохраняем

            try
            {
                using (var writer = new StreamWriter(FileName, false, Encoding.UTF8))
                {
                    for (int i = 0; i < BlocksCount; i++)
                    {
                        int fontSize = _blocksFontSizes[i];
                        if (fontSize <= 0) fontSize = CalculateFont(Blocks[i]);

                        // @NN
                        string str = $"@{(i + 1):00}";
                        // #FFF
                        str += $"#{fontSize:000}";
                        // Текст
                        str += Blocks[i];
                        // $NN
                        str += $"${(i + 1):00}";

                        writer.Write(str);
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Рассчитывает «оптимальный» размер шрифта для текущего блока.
        /// </summary>
        public int CalculateFont()
        {
            if (BlocksCount == 0 || CurrentBlockNumber == 0) return 90;
            return CalculateFont(Blocks[CurrentBlockNumber - 1]);
        }

        /// <summary>
        /// Рассчитывает «оптимальный» размер шрифта для произвольного текста (блока).
        /// </summary>
        private int CalculateFont(string block)
        {
            if (string.IsNullOrWhiteSpace(block))
                return 90; // Заглушка

            // Разбиваем на строки
            var lines = block
                .Split(new[] { '\r', '\n', '#' }, StringSplitOptions.RemoveEmptyEntries);

            // Ищем самую длинную по «весу» строку
            var maxLine = lines.OrderByDescending(s => s.Length).FirstOrDefault() ?? "";
            double weightedLength = 0;

            foreach (char c in maxLine)
            {
                if (_widthCoefficients.ContainsKey(c))
                    weightedLength += _widthCoefficients[c];
                else
                    weightedLength += 1.0; // базовый коэффициент
            }

            // Вычисление базовой ширины в символах
            // symbCountBold – примерная максимальная «длина»
            double symbCountBold = ScreenWidth * 280.0 / 1920.0;
            double fontSizeByWidth = 12.0 * symbCountBold / weightedLength;

            // Высота экрана (16:9 условно)
            double screenHeight = ScreenWidth * 9.0 / 16.0;
            // Приблизительно учитываем межстрочный интервал (1.5)
            double maxLinesOnScreen = screenHeight / (fontSizeByWidth * 1.5);

            // Если строк в блоке больше, чем умещается, уменьшаем
            if (lines.Length > maxLinesOnScreen && maxLinesOnScreen > 0)
            {
                fontSizeByWidth = fontSizeByWidth * (maxLinesOnScreen / lines.Length);
            }

            // Округляем
            int finalSize = (int)fontSizeByWidth;
            if (finalSize < 10) finalSize = 10; // «защита» от слишком маленького
            return finalSize;
        }

        /// <summary>
        /// Рассчитываем размер шрифта для превью (320 – условная ширина превью).
        /// </summary>
        public int CalculatePreviewFontSize(string block)
        {
            int mainFontSize = CalculateFont(block);
            if (ScreenWidth <= 0)
                return mainFontSize; // если вдруг ScreenWidth не задан

            double scaleFactor = 320.0 / ScreenWidth; // масштаб для превью
            int previewFontSize = (int)(mainFontSize * scaleFactor);
            if (previewFontSize < 8) previewFontSize = 8; // нижняя отсечка
            return previewFontSize;
        }

        #endregion

        #region Методы разбивки и «отмены» разбивки

        /// <summary>
        /// Делим «слишком большой» блок на две части, если CalculateFont(block) < FontSizeForSplit.
        /// </summary>
        private List<string> SplitBlockIfNeeded(string block, out List<int> fontSizes)
        {
            var resultBlocks = new List<string>();
            fontSizes = new List<int>();

            // Если рассчитанный шрифт меньше заданного порога – значит блок слишком «большой».
            if (CalculateFont(block) < FontSizeForSplit)
            {
                // Делим по строкам
                var lines = block
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .ToList();

                // Попытка укоротить слишком длинные строки (тоже может быть причиной «большого» блока)
                for (int i = 0; i < lines.Count; i++)
                {
                    if (CalculateFont(lines[i]) < FontSizeForSplit)
                    {
                        int splitPoint = lines[i].Length / 2;
                        // Ищем ближайший пробел справа
                        while (splitPoint < lines[i].Length && lines[i][splitPoint] != ' ')
                        {
                            splitPoint++;
                        }
                        // Если нашли корректный пробел, делим
                        if (splitPoint < lines[i].Length)
                        {
                            var firstHalf = lines[i].Substring(0, splitPoint).Trim();
                            var secondHalf = lines[i].Substring(splitPoint).Trim();
                            lines[i] = firstHalf;
                            lines.Insert(i + 1, secondHalf);
                        }
                    }
                }

                // Теперь «укоротим» блок пополам (по числу строк)
                int mid = lines.Count / 2;
                // Добавим « =>» к первой части, чтобы пометить её как «разбитую»
                var firstPart = string.Join(Environment.NewLine, lines.Take(mid)) + " =>";
                var secondPart = string.Join(Environment.NewLine, lines.Skip(mid));

                resultBlocks.Add(firstPart);
                resultBlocks.Add(secondPart);

                fontSizes.Add(CalculateFont(firstPart));
                fontSizes.Add(CalculateFont(secondPart));
            }
            else
            {
                // Блок достаточно «маленький» – не трогаем
                resultBlocks.Add(block);
                fontSizes.Add(CalculateFont(block));
            }

            return resultBlocks;
        }

        /// <summary>
        /// Автоматическая разбивка всех «слишком больших» блоков.
        /// </summary>
        public void SplitLargeBlocksIfNeeded()
        {
            if (BlocksCount == 0) return;

            // Удаляем «звёздочки» в конце, чтобы не мешали
            RemoveEndSymbols();

            // Проверим, не были ли они уже разбиты (признак " =>").
            // Если хотя бы один блок уже содержит " =>", считаем, что разбивка уже делалась
            if (Blocks.Any(s => s.EndsWith(" =>")))
            {
                AddEndSymbols();
                return;
            }

            var newBlocks = new List<string>();
            var newFontSizes = new List<int>();

            for (int i = 0; i < Blocks.Count; i++)
            {
                string block = Blocks[i];
                int recommendedFont = CalculateFont(block);

                if (recommendedFont < FontSizeForSplit)
                {
                    // Разбиваем
                    List<int> tempFonts;
                    var splitted = SplitBlockIfNeeded(block, out tempFonts);
                    newBlocks.AddRange(splitted);
                    newFontSizes.AddRange(tempFonts);
                }
                else
                {
                    newBlocks.Add(block);
                    newFontSizes.Add(recommendedFont);
                }
            }

            Blocks = newBlocks;
            _blocksFontSizes = newFontSizes;

            AddEndSymbols();
        }

        /// <summary>
        /// Отменяем разбивку по всей песне: ищем блоки, заканчивающиеся на " =>", и объединяем их со следующим.
        /// </summary>
        public void UndoSplitBlocks()
        {
            if (BlocksCount == 0) return;
            RemoveEndSymbols();

            var restoredBlocks = new List<string>();
            var restoredFonts = new List<int>();

            for (int i = 0; i < Blocks.Count; i++)
            {
                string currentBlock = Blocks[i];

                // Признак «первая часть разбитого»
                if (currentBlock.EndsWith(" =>") && i + 1 < Blocks.Count)
                {
                    // Удаляем " =>" и объединяем со следующим
                    string combined = currentBlock.Substring(0, currentBlock.Length - 3).Trim()
                                     + Environment.NewLine
                                     + Blocks[i + 1];

                    restoredBlocks.Add(combined);
                    // Рассчитываем новый размер
                    restoredFonts.Add(CalculateFont(combined));

                    i++; // пропускаем следующий, т.к. он уже объединился
                }
                else
                {
                    restoredBlocks.Add(currentBlock);
                    restoredFonts.Add(_blocksFontSizes[i]);
                }
            }

            Blocks = restoredBlocks;
            _blocksFontSizes = restoredFonts;

            AddEndSymbols();
        }

        /// <summary>
        /// Отменяем разбивку только для конкретного (текущего) блока.
        /// </summary>
        public void UndoSplitForBlock(int blockNumber)
        {
            if (blockNumber <= 0 || blockNumber > BlocksCount)
                return;

            RemoveEndSymbols();

            // Если блок заканчивается на " =>", значит это «первая часть».
            if (Blocks[blockNumber - 1].EndsWith(" =>") && blockNumber < BlocksCount)
            {
                string combined = Blocks[blockNumber - 1].Replace(" =>", "").TrimEnd()
                                 + Environment.NewLine
                                 + Blocks[blockNumber];

                Blocks[blockNumber - 1] = combined;
                _blocksFontSizes[blockNumber - 1] = CalculateFont(combined);

                // Удаляем «следующий» блок
                Blocks.RemoveAt(blockNumber);
                _blocksFontSizes.RemoveAt(blockNumber);
            }

            AddEndSymbols();
        }

        #endregion

        #region Работа с «концевыми» символами (* * *)

        /// <summary>
        /// Удаляем символы типа "* * *" из последнего блока, если они там есть.
        /// </summary>
        private void RemoveEndSymbols()
        {
            if (BlocksCount == 0) return;

            string lastBlock = Blocks[BlocksCount - 1];
            // Удаляем любые * в конце строки
            var modified = Regex.Replace(lastBlock, @"(\*+\s*)+$", "").TrimEnd();
            Blocks[BlocksCount - 1] = modified;
        }

        /// <summary>
        /// Добавляем в конец блока "\n* * *", если таких символов нет.
        /// </summary>
        private void AddEndSymbols()
        {
            if (BlocksCount == 0) return;

            string lastBlock = Blocks[BlocksCount - 1];
            // Если уже содержит "* * *" в конце — не дублируем
            if (!Regex.IsMatch(lastBlock, @"(\*+\s*)+$"))
            {
                Blocks[BlocksCount - 1] = lastBlock + Environment.NewLine + "* * *";
            }
        }

        #endregion

        #region Вставка припева (примерная реализация)

        /// <summary>
        /// Вставляем «припев» после каждого блока, кроме самого припева.
        /// Логика весьма условная и зависит от формата файла/блоков.
        /// </summary>
        public void InsertRefrain()
        {
            if (BlocksCount < 2) return;
            RemoveEndSymbols();

            // Простейшая логика: берём 1-й или 2-й блок в качестве припева
            // (зависит от того, заканчивается ли первый блок на " =>")
            List<string> refrainBlocks = new List<string>();
            if (Blocks[0].EndsWith(" =>"))
            {
                // Допустим, припев это 3-й или 4-й блок
                if (BlocksCount > 3)
                {
                    refrainBlocks.Add(Blocks[2]);
                    refrainBlocks.Add(Blocks[3]);
                }
            }
            else
            {
                // Иначе припев – второй блок
                refrainBlocks.Add(Blocks[1]);
            }

            var updatedBlocks = new List<string>();
            var updatedFonts = new List<int>();

            for (int i = 0; i < Blocks.Count; i++)
            {
                updatedBlocks.Add(Blocks[i]);
                updatedFonts.Add(_blocksFontSizes[i]);

                // Проверяем: если следующий блок не припев (или его нет), вставляем припев
                // Очень условная логика
                bool isCurrentRefrain = refrainBlocks.Contains(Blocks[i]);
                bool nextIsRefrain = (i + 1 < Blocks.Count) && refrainBlocks.Contains(Blocks[i + 1]);

                if (!isCurrentRefrain && !nextIsRefrain)
                {
                    foreach (string rBlock in refrainBlocks)
                    {
                        updatedBlocks.Add(rBlock);
                        updatedFonts.Add(CalculateFont(rBlock));
                    }
                }
            }

            Blocks = updatedBlocks;
            _blocksFontSizes = updatedFonts;

            AddEndSymbols();
        }

        #endregion

        #region Вспомогательные методы

        /// <summary>
        /// Формируем FlowDocument для конкретного блока.
        /// </summary>
        private FlowDocument GetDocument(int blockIndex, bool previewMode)
        {
            var doc = new FlowDocument
            {
                FontFamily = new FontFamily("Arial"),
                IsOptimalParagraphEnabled = true,
                IsHyphenationEnabled = true,
                TextAlignment = System.Windows.TextAlignment.Center,
                PagePadding = new System.Windows.Thickness(0, 40, 0, 40)
            };

            if (blockIndex <= 0 || blockIndex > BlocksCount)
                return doc; // пустой

            string block = Blocks[blockIndex - 1];

            int fontSize = previewMode
                ? CalculatePreviewFontSize(block)
                : _blocksFontSizes[blockIndex - 1];

            if (fontSize <= 0)
                fontSize = CalculateFont(block);

            var paragraph = new Paragraph
            {
                FontSize = fontSize
            };

            // Разбиваем по переводам строк + символ '#'
            var lines = block.Split(new[] { '#', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                paragraph.Inlines.Add(new Bold(new Run(line)));
                paragraph.Inlines.Add(new LineBreak());
            }

            // Удаляем последний LineBreak, если он есть
            if (paragraph.Inlines.LastInline is LineBreak)
            {
                paragraph.Inlines.Remove(paragraph.Inlines.LastInline);
            }

            doc.Blocks.Add(paragraph);
            return doc;
        }

        /// <summary>
        /// Создаём FlowDocument с простым текстом (используется для «<ПУСТО>», «<КОНЕЦ>» и т.д.).
        /// </summary>
        private FlowDocument CreateDocumentWithText(string text)
        {
            var document = new FlowDocument
            {
                FontFamily = new FontFamily("Arial"),
                IsOptimalParagraphEnabled = true,
                IsHyphenationEnabled = true,
                TextAlignment = System.Windows.TextAlignment.Center
            };

            var paragraph = new Paragraph
            {
                Inlines = { new Run(text) }
            };

            document.Blocks.Add(paragraph);
            return document;
        }

        /// <summary>
        /// Удаляем пробелы в конце строки.
        /// </summary>
        private string RemoveTrailingSpaces(string line)
        {
            return line.TrimEnd(' ');
        }

        #endregion
    }
}
