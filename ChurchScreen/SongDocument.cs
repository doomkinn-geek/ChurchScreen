using System;
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
    /// Если в файле уже указаны размеры шрифтов, они используются.
    /// </summary>
    public class SongDocument
    {
        private int _currentBlockNumber;         // Текущий блок (1-based)
        private int _screenHeight;

        public bool ServiceMode { get; private set; }
        public string FileName { get; private set; }

        /// <summary>Основной список текстовых блоков (куплетов/строк) песни.</summary>
        public List<string> Blocks { get; private set; }

        /// <summary>Список размеров шрифта для каждого блока.
        /// Если в файле размер не указан, хранится -1 и затем рассчитывается.</summary>
        private List<int> _blocksFontSizes;

        /// <summary>«Ширина экрана» (в пикселях или DIP), используется для расчёта шрифта.</summary>
        public int ScreenWidth { get; private set; }

        /// <summary>Порог шрифта: если CalculateFont(block) < FontSizeForSplit, блок считается «слишком большим».</summary>
        public int FontSizeForSplit { get; private set; }

        /// <summary>Количество блоков.</summary>
        public int BlocksCount => Blocks?.Count ?? 0;

        /// <summary>Номер текущего блока (1-based), не больше BlocksCount.</summary>
        public int CurrentBlockNumber
        {
            get
            {
                if (BlocksCount == 0) return 0;
                return Math.Min(_currentBlockNumber, BlocksCount);
            }
        }

        /// <summary>Размер шрифта для текущего блока.
        /// Если в файле размер указан (значение > 0), то он используется, иначе – рассчитывается.</summary>
        public int BlockFontSize
        {
            get
            {
                if (CurrentBlockNumber == 0) return 0;
                return _blocksFontSizes[CurrentBlockNumber - 1] > 0
                    ? _blocksFontSizes[CurrentBlockNumber - 1]
                    : CalculateFont();
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

            // Дополняем ".txt", если нужно
            FileName = fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                ? fileName
                : fileName + ".txt";

            // Если файла нет, пробуем в папке "songs"
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

            // Если нет "@01", считаем, что это сервисный режим
            ServiceMode = !fileData.Contains("@01");

            if (ServiceMode)
                LoadTextByEmptyLines(fileData);
            else
                LoadTextAndSplitIntoBlocks(fileData);

            AddEndSymbols();
        }

        private string ReadFileContent(string filePath)
        {
            try
            {
                Encoding encoding = GetFileEncoding(filePath);
                return File.ReadAllText(filePath, encoding);
            }
            catch
            {
                return string.Empty;
            }
        }

        public static Encoding GetFileEncoding(string srcFile)
        {
            using (var file = new FileStream(srcFile, FileMode.Open, FileAccess.Read))
            {
                var buffer = new byte[5];
                file.Read(buffer, 0, 5);

                if (buffer[0] == 0xef && buffer[1] == 0xbb && buffer[2] == 0xbf)
                    return Encoding.UTF8;
                if (buffer[0] == 0xfe && buffer[1] == 0xff)
                    return Encoding.BigEndianUnicode;
                if (buffer[0] == 0 && buffer[1] == 0 && buffer[2] == 0xfe && buffer[3] == 0xff)
                    return Encoding.UTF32;
                if (buffer[0] == 0x2b && buffer[1] == 0x2f && buffer[2] == 0x76)
                    return Encoding.UTF7;
                if (buffer[0] == 0xFF && buffer[1] == 0xFE)
                    return Encoding.Unicode;
            }
            // Если не определили BOM – возвращаем cp1251 (кириллица)
            return Encoding.GetEncoding(1251);
        }

        private void LoadTextAndSplitIntoBlocks(string fileData)
        {
            // Используем регулярное выражение для стандартного формата:
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
            // Разбиваем на блоки по двойным переводам строки
            var rawBlocks = fileData.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
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

        #region Разбивка слишком большого блока (пополам)

        private List<string> SplitBlockIfNecessary(string block, out List<int> fontSizes)
        {
            var result = new List<string>();
            fontSizes = new List<int>();

            // Если рассчитанный шрифт подходит, блок не делим
            if (CalculateFont(block) >= FontSizeForSplit)
            {
                result.Add(block);
                fontSizes.Add(CalculateFont(block));
                return result;
            }

            // Разбиваем блок на строки с сохранением переводов
            var allLines = block.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None).ToList();
            while (allLines.Count > 0 && string.IsNullOrWhiteSpace(allLines[0]))
                allLines.RemoveAt(0);
            while (allLines.Count > 0 && string.IsNullOrWhiteSpace(allLines.Last()))
                allLines.RemoveAt(allLines.Count - 1);

            if (allLines.Count == 0)
            {
                result.Add(block);
                fontSizes.Add(CalculateFont(block));
                return result;
            }

            int mid = allLines.Count / 2;
            var firstLines = allLines.Take(mid).ToList();
            var secondLines = allLines.Skip(mid).ToList();

            if (firstLines.Count == 0)
            {
                firstLines.Add(secondLines[0]);
                secondLines.RemoveAt(0);
            }

            if (firstLines.Count > 0)
            {
                int lastIdx = firstLines.Count - 1;
                firstLines[lastIdx] = firstLines[lastIdx] + " =>";
            }

            string firstPart = string.Join(Environment.NewLine, firstLines);
            string secondPart = string.Join(Environment.NewLine, secondLines);

            int font1 = CalculateFont(firstPart);
            if (font1 < FontSizeForSplit)
            {
                firstPart = TrySplitSingleLineIfNeeded(firstPart);
                font1 = CalculateFont(firstPart);
            }

            int font2 = CalculateFont(secondPart);
            if (font2 < FontSizeForSplit)
            {
                secondPart = TrySplitSingleLineIfNeeded(secondPart);
                font2 = CalculateFont(secondPart);
            }

            result.Add(firstPart);
            fontSizes.Add(font1);
            result.Add(secondPart);
            fontSizes.Add(font2);
            return result;
        }

        private string TrySplitSingleLineIfNeeded(string halfBlock)
        {
            var lines = halfBlock.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None).ToList();
            if (lines.Count > 2)
                return halfBlock;
            bool changed = false;
            for (int i = 0; i < lines.Count; i++)
            {
                if (CalculateFont(lines[i]) < FontSizeForSplit && lines[i].Length > 10)
                {
                    int half = lines[i].Length / 2;
                    while (half < lines[i].Length && lines[i][half] != ' ')
                        half++;
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
            return changed ? string.Join(Environment.NewLine, lines) : halfBlock;
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

        public static FlowDocument CleanDocument() => new FlowDocument();

        #endregion

        #region Новый метод расчёта шрифта (бинарный поиск + measure)

        /// <summary>
        /// Определяет оптимальный размер шрифта, при котором весь текст умещается в области width x height
        /// на одной странице (DocumentPaginator.PageCount == 1) с использованием бинарного поиска.
        /// </summary>
        private int CalculateFontSizeForBlock(string block, double width, double height, int minFont = 10, int maxFont = 200)
        {
            int left = minFont, right = maxFont, bestFit = left;
            while (left <= right)
            {
                int mid = (left + right) / 2;
                if (DoesTextFit(block, width, height, mid))
                {
                    bestFit = mid;
                    left = mid + 1;
                }
                else
                {
                    right = mid - 1;
                }
            }
            return bestFit;
        }

        /// <summary>
        /// Проверяет, умещается ли текст block при fontSize на странице размером width x height.
        /// </summary>
        private bool DoesTextFit(string block, double width, double height, int fontSize)
        {
            FlowDocument doc = new FlowDocument
            {
                FontFamily = new FontFamily("Arial"),
                FontSize = fontSize,
                TextAlignment = TextAlignment.Center,
                PagePadding = new Thickness(0),
                ColumnGap = 0,
                ColumnWidth = double.PositiveInfinity
            };

            var paragraph = new Paragraph();
            var lines = block.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                paragraph.Inlines.Add(new Run(line));
                paragraph.Inlines.Add(new LineBreak());
            }
            doc.Blocks.Add(paragraph);
            doc.PageWidth = width;
            doc.PageHeight = height;

            DocumentPaginator paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
            paginator.PageSize = new Size(width, height);
            paginator.ComputePageCount();
            return paginator.PageCount <= 1;
        }

        #endregion

        #region Обёртки для удобного вызова расчёта шрифта

        /// <summary>
        /// Рассчитывает оптимальный размер шрифта для текущего блока.
        /// Если в файле для этого блока уже указан размер (значение > 0), возвращается он.
        /// </summary>
        public int CalculateFont()
        {
            if (BlocksCount == 0 || CurrentBlockNumber == 0)
                return 90;
            int stored = _blocksFontSizes[CurrentBlockNumber - 1];
            return stored > 0 ? stored : CalculateFont(Blocks[CurrentBlockNumber - 1]);
        }

        /// <summary>
        /// Рассчитывает размер шрифта для заданного блока.
        /// Используется бинарный поиск, если размер не был указан.
        /// </summary>
        private int CalculateFont(string block)
        {
            if (string.IsNullOrWhiteSpace(block)) return 90;
            return CalculateFontSizeForBlock(block, ScreenWidth, _screenHeight);
        }

        /// <summary>
        /// Рассчитывает размер шрифта для предпросмотра.
        /// Если для блока уже задан размер (не –1), он используется, иначе – вычисляется
        /// с параметрами, характерными для previewViewer.
        /// </summary>
        public int CalculatePreviewFontSize(string block)
        {
            // Если размер уже задан для текущего блока, используем его.
            int stored = _blocksFontSizes[CurrentBlockNumber - 1];
            if (stored > 0)
                return stored;
            double previewWidth = 321, previewHeight = 181;
            return CalculateFontSizeForBlock(block, previewWidth, previewHeight);
        }

        #endregion

        #region Сохранение, припев, разбивка блоков и отмена разбивки

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

        public void InsertRefrain()
        {
            if (BlocksCount < 2) return;
            var refrainBlocks = new List<string>();
            // Если первый блок заканчивается на " =>", берем 3-й и 4-й, иначе – второй блок
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

        public void SplitLargeBlocksIfNeeded()
        {
            if (BlocksCount == 0) return;
            RemoveEndSymbols();
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

        public void UndoSplitBlocks()
        {
            if (BlocksCount == 0) return;
            RemoveEndSymbols();
            var restoredBlocks = new List<string>();
            var restoredFonts = new List<int>();
            int i = 0;
            while (i < Blocks.Count)
            {
                string currentBlock = Blocks[i];
                if (currentBlock.EndsWith("=>") && (i + 1) < Blocks.Count)
                {
                    string mergedFirstPart = currentBlock.Replace("=>", "").TrimEnd();
                    string combined = mergedFirstPart + Environment.NewLine + Blocks[i + 1];
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

        public void UndoSplitForBlock(int blockNumber)
        {
            if (blockNumber <= 0 || blockNumber > BlocksCount) return;
            RemoveEndSymbols();
            if (Blocks[blockNumber - 1].EndsWith("=>") && blockNumber < BlocksCount)
            {
                string firstHalf = Blocks[blockNumber - 1].Replace("=>", "").TrimEnd();
                string combined = firstHalf + Environment.NewLine + Blocks[blockNumber];
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

        private FlowDocument GetDocument(int blockIndex, bool previewMode)
        {
            FlowDocument doc = new FlowDocument
            {
                FontFamily = new FontFamily("Arial"),
                IsOptimalParagraphEnabled = true,
                IsHyphenationEnabled = true,
                TextAlignment = TextAlignment.Center,
                PagePadding = new Thickness(0),
                ColumnGap = 0,
                ColumnWidth = double.PositiveInfinity
            };

            if (blockIndex <= 0 || blockIndex > BlocksCount)
                return doc;

            string block = Blocks[blockIndex - 1];
            int fontSize;
            if (previewMode)
                fontSize = CalculatePreviewFontSize(block);
            else
            {
                int storedSize = _blocksFontSizes[blockIndex - 1];
                fontSize = storedSize > 0 ? storedSize : CalculateFont(block);
            }

            var paragraph = new Paragraph { FontSize = fontSize };
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

        #region Вспомогательные методы

        private string RemoveTrailingSpaces(string line)
        {
            return line.TrimEnd(' ');
        }

        private void RemoveEndSymbols()
        {
            if (BlocksCount == 0) return;
            string lastBlock = Blocks[BlocksCount - 1];
            string modified = Regex.Replace(lastBlock, @"(\*+\s*)+$", "").TrimEnd();
            Blocks[BlocksCount - 1] = modified;
        }

        private void AddEndSymbols()
        {
            if (BlocksCount == 0) return;
            string lastBlock = Blocks[BlocksCount - 1];
            if (!Regex.IsMatch(lastBlock, @"(\*+\s*)+$"))
                Blocks[BlocksCount - 1] = lastBlock + Environment.NewLine + "* * *";
        }

        #endregion
    }
}
