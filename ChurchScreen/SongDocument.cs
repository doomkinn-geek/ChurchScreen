﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace ChurchScreen
{
    /// <summary>
    /// Класс, отвечающий за загрузку, хранение, вывод и разбивку песен на блоки,
    /// включая улучшенный расчёт шрифта на основе реального измерения документа (DocumentPaginator).
    /// </summary>
    public class SongDocument
    {
        private int _currentBlockNumber;         // Текущий блок (1-based)
        private int _screenHeight;

        public bool ServiceMode { get; private set; }
        public string FileName { get; private set; }

        /// <summary>Основной список текстовых блоков (куплетов/строк) песни.</summary>
        public List<string> Blocks { get; private set; }

        /// <summary>Размер шрифта для каждого блока (в старом формате @NN#FFF...$NN).</summary>
        private List<int> _blocksFontSizes;

        /// <summary>«Ширина экрана» (в пикселях или DIP), нужна для расчёта шрифта.</summary>
        public int ScreenWidth { get; private set; }

        /// <summary>Порог шрифта: если CalculateFont(block) < FontSizeForSplit => блок «слишком большой».</summary>
        public int FontSizeForSplit { get; private set; }

        /// <summary>Число блоков.</summary>
        public int BlocksCount => Blocks?.Count ?? 0;

        /// <summary>Текущий (1-based), не более BlocksCount.</summary>
        public int CurrentBlockNumber
        {
            get
            {
                if (BlocksCount == 0) return 0;
                return Math.Min(_currentBlockNumber, BlocksCount);
            }
        }

        /// <summary>Шрифт для текущего блока.</summary>
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

        /// <summary>Флаг, что мы вышли за последний блок (CurrentBlockNumber > BlocksCount).</summary>
        public bool IsEnd => _currentBlockNumber > BlocksCount;

        // Конструктор
        public SongDocument(string fileName, int dipWidth, int dipHeight, int fontSizeForSplit)
        {
            ScreenWidth = dipWidth;
            _screenHeight = dipHeight;
            FontSizeForSplit = fontSizeForSplit;
            Initialize(fileName);
        }

        #region Инициализация

        private void Initialize(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return;

            // Дополняем ".txt" если нужно
            FileName = fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                ? fileName
                : fileName + ".txt";

            // Если нет файла, пробуем в папке "songs"
            if (!File.Exists(FileName))
            {
                FileName = System.IO.Path.Combine(
                    Environment.CurrentDirectory,
                    "songs",
                    System.IO.Path.GetFileName(FileName)
                );
                if (!File.Exists(FileName)) return;
            }

            _currentBlockNumber = 0;
            Blocks = new List<string>();
            _blocksFontSizes = new List<int>();

            string fileData = ReadFileContent(FileName);
            if (string.IsNullOrEmpty(fileData)) return;

            // Если нет "@01", считаем ServiceMode
            ServiceMode = !fileData.Contains("@01");

            if (ServiceMode)
                LoadTextByEmptyLines(fileData);
            else
                LoadTextAndSplitIntoBlocks(fileData);

            // Добавим "* * *" в конец (если нужно)
            AddEndSymbols();
        }

        private string ReadFileContent(string filePath)
        {
            try
            {
                Encoding encoding = GetFileEncoding(filePath);
                // Прямо используем эту «нашу» кодировку
                return File.ReadAllText(filePath, encoding);
            }
            catch
            {
                return string.Empty;
            }
        }

        public static Encoding GetFileEncoding(string srcFile)
        {
            // Пробуем определить BOM
            using (var file = new FileStream(srcFile, FileMode.Open, FileAccess.Read))
            {
                var buffer = new byte[5];
                file.Read(buffer, 0, 5);

                // UTF-8 BOM
                if (buffer[0] == 0xef && buffer[1] == 0xbb && buffer[2] == 0xbf)
                    return Encoding.UTF8;

                // Unicode (Big Endian)
                if (buffer[0] == 0xfe && buffer[1] == 0xff)
                    return Encoding.BigEndianUnicode;  // или Encoding.GetEncoding(1201);

                // UTF-32
                if (buffer[0] == 0 && buffer[1] == 0 && buffer[2] == 0xfe && buffer[3] == 0xff)
                    return Encoding.UTF32;

                // UTF-7
                if (buffer[0] == 0x2b && buffer[1] == 0x2f && buffer[2] == 0x76)
                    return Encoding.UTF7;

                // UTF-16 (LE)
                if (buffer[0] == 0xFF && buffer[1] == 0xFE)
                    return Encoding.Unicode; // или Encoding.GetEncoding(1200)
            }

            // Если мы сюда дошли – нет BOM, предполагаем «ANSI».
            // Но в .NET 6 Encoding.Default может не быть cp1251, берём 1251:
            return Encoding.GetEncoding(1251);
        }

        private void LoadTextAndSplitIntoBlocks(string fileData)
        {
            var pattern = @"@\d{2}#(\d{3})(.*?)\$\d{2}";
            var matches = Regex.Matches(fileData, pattern, RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                string textBlock = match.Groups[2].Value.Trim();
                string fontSizeStr = match.Groups[1].Value;

                Blocks.Add(textBlock);
                if (int.TryParse(fontSizeStr, out int fs))
                    _blocksFontSizes.Add(fs);
                else
                    _blocksFontSizes.Add(-1);
            }

            if (Blocks.Count != _blocksFontSizes.Count)
                ResetSongData();
        }

        private void LoadTextByEmptyLines(string fileData)
        {
            // Разбиваем «блоки» по двум переводам строки
            var rawBlocks = fileData
                .Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(b => b.Trim())
                .ToArray();

            foreach (string rawBlock in rawBlocks)
            {
                string cleaned = RemoveTrailingSpaces(rawBlock);
                var splitted = SplitBlockIfNecessary(cleaned, out List<int> splittedSizes);

                Blocks.AddRange(splitted);
                _blocksFontSizes.AddRange(splittedSizes);
            }

            if (Blocks.Count != _blocksFontSizes.Count)
                ResetSongData();
        }

        private void ResetSongData()
        {
            _currentBlockNumber = 0;
            Blocks?.Clear();
            _blocksFontSizes?.Clear();
        }

        #endregion

        #region Разбивка слишком большого блока (только пополам)

        /// <summary>
        /// Если CalculateFont(block) < FontSizeForSplit => делим блок ровно на 2 части (по числу строк).
        /// В конец первой части добавляем " =>" (без лишних переводов строки).
        /// Кроме того, если часть очень мала (1-2 строки) и всё ещё не «влезает»,
        /// можем один раз разрезать конкретную строку пополам.
        /// </summary>
        private List<string> SplitBlockIfNecessary(string block, out List<int> fontSizes)
        {
            var result = new List<string>();
            fontSizes = new List<int>();

            // Если шрифт «достаточный» => не делим
            if (CalculateFont(block) >= FontSizeForSplit)
            {
                result.Add(block);
                fontSizes.Add(CalculateFont(block));
                return result;
            }

            // 1) Разбиваем блок на строки (сохраняем переводы строк).
            var allLines = block.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None).ToList();

            // Уберём лишние пустые строки в начале/конце
            while (allLines.Count > 0 && string.IsNullOrWhiteSpace(allLines[0]))
                allLines.RemoveAt(0);
            while (allLines.Count > 0 && string.IsNullOrWhiteSpace(allLines[allLines.Count - 1]))
                allLines.RemoveAt(allLines.Count - 1);

            if (allLines.Count == 0)
            {
                // Пустой блок?
                result.Add(block);
                fontSizes.Add(CalculateFont(block));
                return result;
            }

            // 2) Делим по числу строк пополам
            int mid = allLines.Count / 2;  // целочисленно
            var firstLines = allLines.Take(mid).ToList();
            var secondLines = allLines.Skip(mid).ToList();

            // Гарантируем, что в первой части есть хотя бы 1 строка
            if (firstLines.Count == 0)
            {
                // Перестраховка, если строк было очень мало
                firstLines.Add(secondLines[0]);
                secondLines.RemoveAt(0);
            }

            // 3) В конец последней строки первой части добавляем " =>"
            if (firstLines.Count > 0)
            {
                int lastIdx = firstLines.Count - 1;
                firstLines[lastIdx] = firstLines[lastIdx] + " =>";
            }

            // 4) Собираем «половинки» обратно
            string firstPart = string.Join(Environment.NewLine, firstLines);
            string secondPart = string.Join(Environment.NewLine, secondLines);

            // 5) Проверяем, не слишком ли маленький шрифт у первой части
            int font1 = CalculateFont(firstPart);
            if (font1 < FontSizeForSplit)
            {
                firstPart = TrySplitSingleLineIfNeeded(firstPart);
                font1 = CalculateFont(firstPart);
            }

            // Аналогично для второй части
            int font2 = CalculateFont(secondPart);
            if (font2 < FontSizeForSplit)
            {
                secondPart = TrySplitSingleLineIfNeeded(secondPart);
                font2 = CalculateFont(secondPart);
            }

            // Добавляем в результат ровно 2 части
            result.Add(firstPart);
            fontSizes.Add(font1);

            result.Add(secondPart);
            fontSizes.Add(font2);

            return result;
        }

        /// <summary>
        /// Если часть состоит из 1-2 строк и всё ещё «не влезает»,
        /// пытаемся разрезать каждую строку ровно один раз посередине (по ближайшему пробелу).
        /// </summary>
        private string TrySplitSingleLineIfNeeded(string halfBlock)
        {
            var lines = halfBlock.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None).ToList();
            if (lines.Count > 2)
                return halfBlock;  // не трогаем, если строк больше 2

            bool changed = false;
            for (int i = 0; i < lines.Count; i++)
            {
                if (CalculateFont(lines[i]) < FontSizeForSplit && lines[i].Length > 10)
                {
                    // Разрезаем строку пополам по пробелу
                    int half = lines[i].Length / 2;
                    while (half < lines[i].Length && lines[i][half] != ' ')
                    {
                        half++;
                    }
                    if (half < lines[i].Length)
                    {
                        string p1 = lines[i].Substring(0, half).TrimEnd();
                        string p2 = lines[i].Substring(half).TrimStart();

                        lines[i] = p1;
                        lines.Insert(i + 1, p2);
                        i++;
                        changed = true;
                    }
                }
            }

            if (!changed)
                return halfBlock;

            return string.Join(Environment.NewLine, lines);
        }

        #endregion

        #region Методы доступа: Next, Prev, First, Current, ToMainScreen

        public FlowDocument FirstBlock()
        {
            if (BlocksCount == 0)
                return CreateDocumentWithText("<ПУСТО>");

            _currentBlockNumber = 1;
            return GetDocument(_currentBlockNumber, previewMode: true);
        }

        public FlowDocument CurrentBlock()
        {
            if (BlocksCount == 0)
                return CreateDocumentWithText("<ПУСТО>");

            return GetDocument(CurrentBlockNumber, previewMode: true);
        }

        public FlowDocument NextBlock()
        {
            if (BlocksCount == 0)
                return CreateDocumentWithText("<ПУСТО>");

            if (_currentBlockNumber >= BlocksCount)
            {
                _currentBlockNumber = BlocksCount + 1;
                return CreateDocumentWithText("<КОНЕЦ>");
            }

            _currentBlockNumber++;
            return GetDocument(_currentBlockNumber, previewMode: true);
        }

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

        public FlowDocument ToMainScreen()
        {
            if (BlocksCount == 0)
                return CreateDocumentWithText("<ПУСТО>");

            return GetDocument(CurrentBlockNumber, previewMode: false);
        }

        public static FlowDocument CleanDocument()
        {
            return new FlowDocument();
        }

        #endregion

        #region Новый метод расчёта шрифта (бинарный поиск + реальный measure)

        /// <summary>
        /// Определяет «максимальный» размер шрифта, при котором весь текст умещается
        /// в области width x height на одной «виртуальной» странице (DocumentPaginator.PageCount == 1).
        /// </summary>
        private int CalculateFontSizeForBlock(string block, double width, double height, int minFont = 10, int maxFont = 200)
        {
            int left = minFont;
            int right = maxFont;
            int bestFit = left;

            while (left <= right)
            {
                int mid = (left + right) / 2;
                if (DoesTextFit(block, width, height, mid))
                {
                    bestFit = mid;
                    left = mid + 1;     // пробуем больше
                }
                else
                {
                    right = mid - 1;    // уменьшаем
                }
            }
            return bestFit;
        }

        /// <summary>
        /// Проверяем, умещается ли строка block целиком на одной странице width x height при fontSize.
        /// </summary>
        private bool DoesTextFit(string block, double width, double height, int fontSize)
        {
            // Создаём FlowDocument без отступов и колоночной верстки
            FlowDocument doc = new FlowDocument
            {
                FontFamily = new FontFamily("Arial"),
                FontSize = fontSize,
                TextAlignment = TextAlignment.Center,
                PagePadding = new Thickness(0),
                ColumnGap = 0,
                ColumnWidth = double.PositiveInfinity
            };

            // Формируем абзац
            var paragraph = new Paragraph();
            // Разбиваем по строкам (с учётом, если нужно, пустых)
            var lines = block.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                paragraph.Inlines.Add(new Run(line));
                paragraph.Inlines.Add(new LineBreak());
            }
            doc.Blocks.Add(paragraph);

            // Устанавливаем виртуальный размер страницы
            doc.PageWidth = width;
            doc.PageHeight = height;

            // Получаем paginator
            DocumentPaginator paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
            paginator.PageSize = new Size(width, height);

            // Обновляем кол-во страниц
            paginator.ComputePageCount();

            // Если больше 1 страницы - не умещается
            return paginator.PageCount <= 1;
        }

        #endregion

        #region Обёртки для удобного вызова

        /// <summary>
        /// Рассчитывает оптимальный размер шрифта для текущего блока
        /// (использует размеры _screenWidth / _screenHeight).
        /// </summary>
        public int CalculateFont()
        {
            if (BlocksCount == 0 || CurrentBlockNumber == 0)
                return 90;

            return CalculateFont(Blocks[CurrentBlockNumber - 1]);
        }

        /// <summary>
        /// То же самое, но для произвольного блока (строки).
        /// </summary>
        private int CalculateFont(string block)
        {
            // Вызываем бинарный поиск
            return CalculateFontSizeForBlock(block, ScreenWidth, _screenHeight);
        }

        /// <summary>
        /// Аналогично, но для предпросмотра (можно передавать фиксированную ширину previewViewer).
        /// Если у вас previewViewer.Width = 320, previewViewer.Height = 180 и т.д.,
        /// подставьте сюда реальные размеры.
        /// </summary>
        public int CalculatePreviewFontSize(string block)
        {
            // Допустим, фиксированная ширина 321, высота 181 (пример).
            // При желании можете вычислять/привязывать реальные размеры previewViewer.
            double previewWidth = 321;
            double previewHeight = 181;

            return CalculateFontSizeForBlock(block, previewWidth, previewHeight);
        }

        #endregion

        #region Сохранение, припев, SplitLargeBlocksIfNeeded, UndoSplit...

        /// <summary>
        /// Сохранение песни в формате @NN#FFF(текст)$NN.
        /// </summary>
        public bool SaveSong()
        {
            if (BlocksCount == 0)
                return false;
            try
            {
                using (var writer = new StreamWriter(FileName, false, Encoding.UTF8))
                {
                    for (int i = 0; i < BlocksCount; i++)
                    {
                        int fs = _blocksFontSizes[i];
                        if (fs <= 0)
                            fs = CalculateFont(Blocks[i]);

                        string str = $"@{(i + 1):00}#{fs:000}{Blocks[i]}${(i + 1):00}";
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
        /// Примерная вставка «припева».
        /// </summary>
        public void InsertRefrain()
        {
            if (BlocksCount < 2) return;
            RemoveEndSymbols();

            var refrainBlocks = new List<string>();
            if (Blocks[0].EndsWith(" =>"))
            {
                if (BlocksCount > 3)
                {
                    refrainBlocks.Add(Blocks[2]);
                    refrainBlocks.Add(Blocks[3]);
                }
            }
            else
            {
                // Иначе второй блок
                refrainBlocks.Add(Blocks[1]);
            }

            var updated = new List<string>();
            var updatedFonts = new List<int>();

            for (int i = 0; i < Blocks.Count; i++)
            {
                updated.Add(Blocks[i]);
                updatedFonts.Add(_blocksFontSizes[i]);

                bool isCurRef = refrainBlocks.Contains(Blocks[i]);
                bool nextRef = (i + 1 < Blocks.Count) && refrainBlocks.Contains(Blocks[i + 1]);
                if (!isCurRef && !nextRef)
                {
                    foreach (var rBlock in refrainBlocks)
                    {
                        updated.Add(rBlock);
                        updatedFonts.Add(CalculateFont(rBlock));
                    }
                }
            }

            Blocks = updated;
            _blocksFontSizes = updatedFonts;
            AddEndSymbols();
        }

        /// <summary>
        /// Разбиваем все большие блоки (если шрифт слишком мелкий).
        /// </summary>
        public void SplitLargeBlocksIfNeeded()
        {
            if (BlocksCount == 0) return;

            RemoveEndSymbols();
            // Если уже есть " =>" — считаем, что уже разбивали
            if (Blocks.Any(s => s.EndsWith(" =>")))
            {
                AddEndSymbols();
                return;
            }

            var newBlocks = new List<string>();
            var newFonts = new List<int>();

            for (int i = 0; i < Blocks.Count; i++)
            {
                string block = Blocks[i];
                int recFont = CalculateFont(block);
                if (recFont < FontSizeForSplit)
                {
                    var splitted = SplitBlockIfNecessary(block, out List<int> splittedSizes);
                    newBlocks.AddRange(splitted);
                    newFonts.AddRange(splittedSizes);
                }
                else
                {
                    newBlocks.Add(block);
                    newFonts.Add(recFont);
                }
            }

            Blocks = newBlocks;
            _blocksFontSizes = newFonts;
            AddEndSymbols();
        }

        /// <summary>
        /// Так как мы храним " =>" в конце первой половины, можно сделать UndoSplit,
        /// склеивая пары блоков. Если нужно.
        /// </summary>
        public void UndoSplitBlocks()
        {
            if (BlocksCount == 0) return;
            RemoveEndSymbols(); // убираем "* * *" из конца, если есть

            var restoredBlocks = new List<string>();
            var restoredFonts = new List<int>();

            int i = 0;
            while (i < Blocks.Count)
            {
                string currentBlock = Blocks[i];

                // Если текущий блок заканчивается на " =>" и есть следующий
                if (currentBlock.EndsWith("=>") && (i + 1) < Blocks.Count)
                {
                    // Удаляем " =>" из конца
                    string mergedFirstPart = currentBlock.Replace("=>", "").TrimEnd();

                    // Склеиваем с блоком i+1
                    string combined = mergedFirstPart
                                      + Environment.NewLine
                                      + Blocks[i + 1];

                    int combinedFont = CalculateFont(combined);

                    restoredBlocks.Add(combined);
                    restoredFonts.Add(combinedFont);

                    i += 2;
                }
                else
                {
                    restoredBlocks.Add(currentBlock);
                    restoredFonts.Add(_blocksFontSizes[i]);
                    i++;
                }
            }

            Blocks = restoredBlocks;
            _blocksFontSizes = restoredFonts;

            AddEndSymbols();
        }

        /// <summary>
        /// Отменить разбивку только одного блока (если " =>" в конце).
        /// </summary>
        public void UndoSplitForBlock(int blockNumber)
        {
            if (blockNumber <= 0 || blockNumber > BlocksCount) return;
            RemoveEndSymbols();

            if (Blocks[blockNumber - 1].EndsWith("=>") && blockNumber < BlocksCount)
            {
                string firstHalf = Blocks[blockNumber - 1].Replace("=>", "").TrimEnd();
                string combined = firstHalf
                                  + Environment.NewLine
                                  + Blocks[blockNumber];
                int fsize = CalculateFont(combined);

                Blocks[blockNumber - 1] = combined;
                _blocksFontSizes[blockNumber - 1] = fsize;

                Blocks.RemoveAt(blockNumber);
                _blocksFontSizes.RemoveAt(blockNumber);
            }

            AddEndSymbols();
        }

        #endregion

        #region Формирование FlowDocument

        /// <summary>
        /// Возвращает FlowDocument для блока (blockIndex).
        /// Если previewMode = true, используем расчёт шрифта для предпросмотра (CalculatePreviewFontSize).
        /// Иначе берём хранимый _blocksFontSizes (или пересчитываем).
        /// </summary>
        private FlowDocument GetDocument(int blockIndex, bool previewMode)
        {
            FlowDocument doc = new FlowDocument
            {
                FontFamily = new FontFamily("Arial"),
                IsOptimalParagraphEnabled = true,
                IsHyphenationEnabled = true,
                TextAlignment = TextAlignment.Center,
                PagePadding = new Thickness(0),  // без отступов
                ColumnGap = 0,
                ColumnWidth = double.PositiveInfinity
            };

            if (blockIndex <= 0 || blockIndex > BlocksCount)
                return doc; // пусто

            string block = Blocks[blockIndex - 1];
            int fontSize;
            if (previewMode)
            {
                fontSize = CalculatePreviewFontSize(block);
            }
            else
            {
                int storedSize = _blocksFontSizes[blockIndex - 1];
                fontSize = storedSize > 0 ? storedSize : CalculateFont(block);
            }

            var paragraph = new Paragraph { FontSize = fontSize };

            // Разбиваем на строки (старый подход - убираем пустые).
            var lines = block.Split(new[] { '#', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                paragraph.Inlines.Add(new Bold(new Run(line)));
                paragraph.Inlines.Add(new LineBreak());
            }

            if (paragraph.Inlines.LastInline is LineBreak)
                paragraph.Inlines.Remove(paragraph.Inlines.LastInline);

            doc.Blocks.Add(paragraph);
            return doc;
        }

        /// <summary>Создаём FlowDocument с простым текстом, на случай "<ПУСТО>".</summary>
        private FlowDocument CreateDocumentWithText(string text)
        {
            var document = new FlowDocument
            {
                FontFamily = new FontFamily("Arial"),
                IsOptimalParagraphEnabled = true,
                IsHyphenationEnabled = true,
                TextAlignment = TextAlignment.Center,
                PagePadding = new Thickness(0),
                ColumnGap = 0,
                ColumnWidth = double.PositiveInfinity
            };

            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run(text));
            document.Blocks.Add(paragraph);
            return document;
        }

        #endregion

        #region Вспомогательные

        private string RemoveTrailingSpaces(string line)
        {
            return line.TrimEnd(' ');
        }

        /// <summary>
        /// Удаляем "* * *" из конца последнего блока (если есть).
        /// </summary>
        private void RemoveEndSymbols()
        {
            if (BlocksCount == 0) return;
            string lastBlock = Blocks[BlocksCount - 1];
            string modified = Regex.Replace(lastBlock, @"(\*+\s*)+$", "").TrimEnd();
            Blocks[BlocksCount - 1] = modified;
        }

        /// <summary>
        /// Добавляем "* * *" в конец (если нет).
        /// </summary>
        private void AddEndSymbols()
        {
            if (BlocksCount == 0) return;
            string lastBlock = Blocks[BlocksCount - 1];
            if (!Regex.IsMatch(lastBlock, @"(\*+\s*)+$"))
            {
                Blocks[BlocksCount - 1] = lastBlock + Environment.NewLine + "* * *";
            }
        }

        #endregion
    }
}
