using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// Класс, отвечающий за загрузку, хранение, вывод и разбивку песен на блоки.
    /// </summary>
    public class SongDocument
    {
        private int _currentBlockNumber;         // Текущий блок (1-based)
        public bool ServiceMode { get; private set; }
        public string FileName { get; private set; }

        /// <summary>Основной список текстовых блоков (куплетов/строк) песни.</summary>
        public List<string> Blocks { get; private set; }

        /// <summary>Размер шрифта для каждого блока (в старом формате @NN#FFF...$NN).</summary>
        private List<int> _blocksFontSizes;

        /// <summary>Ширина экрана (в пикселях), которая нужна для расчёта шрифта.</summary>
        public int ScreenWidth { get; private set; }

        /// <summary>Пороговый размер шрифта: если меньше — считаем «слишком большой» блок и разбиваем.</summary>
        public int FontSizeForSplit { get; private set; }

        /// <summary>Число блоков в песне.</summary>
        public int BlocksCount => Blocks?.Count ?? 0;

        /// <summary>Номер текущего блока (1-based), но не более количества блоков.</summary>
        public int CurrentBlockNumber
        {
            get
            {
                if (BlocksCount == 0) return 0;
                return Math.Min(_currentBlockNumber, BlocksCount);
            }
        }

        /// <summary>Размер шрифта для текущего блока (если 0, значит нет блоков).</summary>
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

        /// <summary>Флаг, означающий, что мы вышли за границы последнего блока.</summary>
        public bool IsEnd => _currentBlockNumber > BlocksCount;

        /// <summary>
        /// Словарь «весов» символов, чтобы прикинуть длину самых широких строк.
        /// </summary>
        private Dictionary<char, double> widthCoefficients = new Dictionary<char, double>
        {
            // Латинские
            { 'W', 1.5 }, { 'M', 1.4 }, { 'm', 1.3 }, { 'w', 1.3 },
            { 'i', 0.7 }, { 'l', 0.6 }, { 'j', 0.6 }, { 't', 0.8 }, { 'f', 0.8 }, { 'r', 0.9 },

            // Кириллические
            { 'Ш', 1.4 }, { 'М', 1.4 }, { 'м', 1.3 }, { 'ш', 1.3 }, { 'щ', 1.5 }, { 'ф', 1.3 },
            { 'й', 0.8 }, { 'л', 0.9 }, { 'т', 0.9 }, { 'и', 0.9 },

            // Цифры и знаки препинания
            { '1', 0.8 }, { '.', 0.6 }, { ',', 0.6 }, { ':', 0.6 }, { ';', 0.6 }, { '!', 0.7 },
        };

        public SongDocument(string fileName, int screenWidth, int fontSizeForSplit)
        {
            FontSizeForSplit = fontSizeForSplit;
            ScreenWidth = screenWidth;
            Initialize(fileName);
        }

        #region Инициализация и загрузка из файла

        private void Initialize(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return;

            // Добавим ".txt", если нужно
            FileName = fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                ? fileName
                : fileName + ".txt";

            // Если файл не найден, проверим папку "songs"
            if (!File.Exists(FileName))
            {
                FileName = Path.Combine(Environment.CurrentDirectory, "songs", Path.GetFileName(FileName));
                if (!File.Exists(FileName)) return;
            }

            _currentBlockNumber = 0;
            Blocks = new List<string>();
            _blocksFontSizes = new List<int>();

            // Считываем файл (автоматическая кодировка)
            string fileData = ReadFileContent(FileName);
            if (string.IsNullOrEmpty(fileData)) return;

            // Проверяем, содержит ли файл спецтег @01 => если нет, значит ServiceMode
            ServiceMode = !fileData.Contains("@01");

            if (ServiceMode)
            {
                // Разбиваем блоки по двойным переводам строки
                LoadTextByEmptyLines(fileData);
            }
            else
            {
                // Обычные файлы c форматом @NN#FFF(текст)$NN
                LoadTextAndSplitIntoBlocks(fileData);
            }

            // Добавим "* * *" в конец (при необходимости)
            AddEndSymbols();
        }

        /// <summary>Считывание файла, определение кодировки (BOM) и возврат текста.</summary>
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

        /// <summary>Автоопределение кодировки по BOM.</summary>
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

                return Encoding.Default;
            }
        }

        /// <summary>
        /// Загрузка обычного (несервисного) файла:
        /// Ищем блоки @NN#FFF(текст)$NN, где NN - номер блока, FFF - размер шрифта.
        /// </summary>
        private void LoadTextAndSplitIntoBlocks(string fileData)
        {
            var blockPattern = @"@\d{2}#(\d{3})(.*?)\$\d{2}";
            var matches = Regex.Matches(fileData, blockPattern, RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                var textBlock = match.Groups[2].Value.Trim();
                var fontSizeStr = match.Groups[1].Value;

                Blocks.Add(textBlock);
                if (int.TryParse(fontSizeStr, out int fontSize))
                    _blocksFontSizes.Add(fontSize);
                else
                    _blocksFontSizes.Add(-1);
            }

            // Если почему-то не совпало количество
            if (Blocks.Count != _blocksFontSizes.Count)
            {
                ResetSongData();
            }
        }

        /// <summary>
        /// Сервисный режим: блоки разделяются двумя пустыми строками (\r\n\r\n).
        /// </summary>
        private void LoadTextByEmptyLines(string fileData)
        {
            var rawBlocks = fileData
                .Split(new string[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(b => b.Trim())
                .ToArray();

            foreach (var rawBlock in rawBlocks)
            {
                var cleanedBlock = RemoveTrailingSpaces(rawBlock);
                var splittedBlocks = SplitBlockIfNecessary(cleanedBlock, out List<int> splitSizes);

                Blocks.AddRange(splittedBlocks);
                _blocksFontSizes.AddRange(splitSizes);
            }

            if (Blocks.Count != _blocksFontSizes.Count)
            {
                ResetSongData();
            }
        }

        /// <summary>Сброс, если встретили несоответствия.</summary>
        private void ResetSongData()
        {
            _currentBlockNumber = 0;
            Blocks?.Clear();
            _blocksFontSizes?.Clear();
        }

        #endregion

        #region Базовый метод разбивки больших блоков (старый)

        /// <summary>
        /// Если у блока рассчитанный размер шрифта (CalculateFont) < FontSizeForSplit —
        /// значит, блок слишком большой. Разделим его на две части.
        /// </summary>
        private List<string> SplitBlockIfNecessary(string block, out List<int> fontSizes)
        {
            var result = new List<string>();
            fontSizes = new List<int>();

            // Если шрифт получается слишком мелким => блок слишком большой => разбиваем
            if (CalculateFont(block) < FontSizeForSplit)
            {
                // Разбиваем по строкам (убираем пустые)
                var lines = block
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .ToList();

                // Попытка дополнительно разрезать слишком длинные строки
                for (int i = 0; i < lines.Count; i++)
                {
                    if (CalculateFont(lines[i]) < FontSizeForSplit)
                    {
                        int splitPoint = lines[i].Length / 2;
                        // Ищем пробел, чтобы разделить строку
                        while (splitPoint < lines[i].Length && lines[i][splitPoint] != ' ')
                        {
                            splitPoint++;
                        }
                        if (splitPoint < lines[i].Length)
                        {
                            var firstHalf = lines[i].Substring(0, splitPoint).Trim();
                            var secondHalf = lines[i].Substring(splitPoint).Trim();

                            lines[i] = firstHalf;
                            lines.Insert(i + 1, secondHalf);
                        }
                    }
                }

                // Теперь делим блок примерно пополам
                int mid = lines.Count / 2;
                var firstPart = string.Join(Environment.NewLine, lines.Take(mid)) + " =>";
                var secondPart = string.Join(Environment.NewLine, lines.Skip(mid));

                result.Add(firstPart);
                result.Add(secondPart);

                fontSizes.Add(CalculateFont(firstPart));
                fontSizes.Add(CalculateFont(secondPart));
            }
            else
            {
                // Если блок «не слишком большой», просто добавляем как есть
                result.Add(block);
                fontSizes.Add(CalculateFont(block));
            }

            return result;
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

        #region Старый метод расчёта шрифта (из «старой» версии)

        /// <summary>
        /// Рассчитывает «оптимальный» размер шрифта для текущего блока (если он есть).
        /// </summary>
        public int CalculateFont()
        {
            if (BlocksCount == 0 || CurrentBlockNumber == 0) return 90;
            return CalculateFont(Blocks[CurrentBlockNumber - 1]);
        }

        /// <summary>
        /// Именно «старая» логика: ищем самую длинную строку по количеству символов
        /// с учётом weightCoefficients, затем подбираем fontSizeByWidth,
        /// и дополнительно корректируем по количеству строк (maxLinesOnScreen).
        /// </summary>
        private int CalculateFont(string block)
        {
            if (string.IsNullOrWhiteSpace(block))
                return 90;

            // Разбиваем на строки (убираем пустые)
            var lines = block.Split(new[] { '#', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var maxLengthStr = lines.OrderByDescending(s => s.Length).FirstOrDefault() ?? string.Empty;

            // «Взвешенная длина» (широкие символы дают больший вклад)
            double weightedLength = 0;
            foreach (char c in maxLengthStr)
            {
                if (widthCoefficients.TryGetValue(c, out double coeff))
                    weightedLength += coeff;
                else
                    weightedLength += 1;
            }

            // Используем эмпирическую формулу (ScreenWidth * 280 / 1920)
            double symbCountBold = ScreenWidth * 280.0 / 1920.0;
            double fontSizeByWidth = 12.0 * symbCountBold / weightedLength;

            // Прикидываем «высоту» (16:9) относительно ScreenWidth
            double screenHeight = ScreenWidth * 9.0 / 16.0;
            // Межстрочный интервал ≈ 1.5
            double maxLinesOnScreen = screenHeight / (fontSizeByWidth * 1.5);

            // Если строк больше, чем помещается, уменьшаем
            if (lines.Length > maxLinesOnScreen && maxLinesOnScreen > 0)
            {
                fontSizeByWidth = fontSizeByWidth * (maxLinesOnScreen / lines.Length);
            }

            int finalSize = (int)fontSizeByWidth;
            if (finalSize < 10) finalSize = 10; // не уходим меньше 10
            return finalSize;
        }

        /// <summary>
        /// Уменьшенный шрифт для «превью».
        /// </summary>
        public int CalculatePreviewFontSize(string block)
        {
            int mainFontSize = CalculateFont(block);
            if (ScreenWidth <= 0) return mainFontSize;

            // Масштабируем, чтобы на «превью» (ширина 320) всё выглядело мельче
            double scaleFactor = 320.0 / ScreenWidth;
            int previewFontSize = (int)(mainFontSize * scaleFactor);
            if (previewFontSize < 8) previewFontSize = 8;
            return previewFontSize;
        }

        #endregion

        #region Методы сохранения, вставки припева, разбивки и т.д.

        /// <summary>Сохранение песни в формате @NN#FFF(текст)$NN.</summary>
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
                        int fontSize = _blocksFontSizes[i];
                        if (fontSize <= 0)
                            fontSize = CalculateFont(Blocks[i]);

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
        /// Вставка «припева» (примерная логика) — берем 2-й или 3-й блок и вставляем его после каждого куплета.
        /// </summary>
        public void InsertRefrain()
        {
            if (BlocksCount < 2) return;
            RemoveEndSymbols();

            List<string> refrainBlocks = new List<string>();
            if (Blocks[0].EndsWith(" =>"))
            {
                // Допустим, припев — 3 и 4 блок
                if (BlocksCount > 3)
                {
                    refrainBlocks.Add(Blocks[2]);
                    refrainBlocks.Add(Blocks[3]);
                }
            }
            else
            {
                // Иначе припев — это второй блок
                refrainBlocks.Add(Blocks[1]);
            }

            var updatedBlocks = new List<string>();
            var updatedFonts = new List<int>();

            for (int i = 0; i < Blocks.Count; i++)
            {
                updatedBlocks.Add(Blocks[i]);
                updatedFonts.Add(_blocksFontSizes[i]);

                // Простейшая проверка: если следующий не припев — вставляем припев
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

        /// <summary>Разбиваем все большие блоки, если шрифт слишком маленький.</summary>
        public void SplitLargeBlocksIfNeeded()
        {
            if (BlocksCount == 0) return;

            RemoveEndSymbols();
            // Проверяем, не разбито ли уже (признак " =>")
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
                int recommendedFont = CalculateFont(block);

                if (recommendedFont < FontSizeForSplit)
                {
                    // Разбиваем
                    var splitted = SplitBlockIfNecessary(block, out List<int> tempFonts);
                    newBlocks.AddRange(splitted);
                    newFonts.AddRange(tempFonts);
                }
                else
                {
                    newBlocks.Add(block);
                    newFonts.Add(recommendedFont);
                }
            }

            Blocks = newBlocks;
            _blocksFontSizes = newFonts;
            AddEndSymbols();
        }

        /// <summary>Отменяем разбивку: если блок заканчивается " =>", склеиваем с следующим.</summary>
        public void UndoSplitBlocks()
        {
            if (BlocksCount == 0) return;
            RemoveEndSymbols();

            var restoredBlocks = new List<string>();
            var restoredFonts = new List<int>();

            for (int i = 0; i < Blocks.Count; i++)
            {
                string currentBlock = Blocks[i];
                if (currentBlock.EndsWith(" =>") && i + 1 < Blocks.Count)
                {
                    // Удаляем " =>" и склеиваем со следующим
                    string combined = currentBlock.Replace(" =>", "").TrimEnd()
                                     + Environment.NewLine
                                     + Blocks[i + 1];

                    restoredBlocks.Add(combined);
                    restoredFonts.Add(CalculateFont(combined));
                    i++;
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

        /// <summary>Отменить разбивку только для одного блока (если он заканчивается " =>").</summary>
        public void UndoSplitForBlock(int blockNumber)
        {
            if (blockNumber <= 0 || blockNumber > BlocksCount) return;

            RemoveEndSymbols();
            // Если блок заканчивается " =>" — это первая часть
            if (Blocks[blockNumber - 1].EndsWith(" =>") && blockNumber < BlocksCount)
            {
                string combined = Blocks[blockNumber - 1].Replace(" =>", "").TrimEnd()
                                 + Environment.NewLine
                                 + Blocks[blockNumber];

                Blocks[blockNumber - 1] = combined;
                _blocksFontSizes[blockNumber - 1] = CalculateFont(combined);

                Blocks.RemoveAt(blockNumber);
                _blocksFontSizes.RemoveAt(blockNumber);
            }

            AddEndSymbols();
        }

        #endregion

        #region Методы вспомогательные

        /// <summary>Формируем FlowDocument для конкретного блока.</summary>
        private FlowDocument GetDocument(int blockIndex, bool previewMode)
        {
            var doc = new FlowDocument
            {
                FontFamily = new FontFamily("Arial"),
                IsOptimalParagraphEnabled = true,
                IsHyphenationEnabled = true,
                TextAlignment = TextAlignment.Center,
                PagePadding = new Thickness(0, 40, 0, 40)
            };

            if (blockIndex <= 0 || blockIndex > BlocksCount)
                return doc; // пусто

            string block = Blocks[blockIndex - 1];

            int fontSize = previewMode
                ? CalculatePreviewFontSize(block)
                : _blocksFontSizes[blockIndex - 1];

            if (fontSize <= 0)
                fontSize = CalculateFont(block);

            var paragraph = new Paragraph { FontSize = fontSize };

            // «Старый» подход к разбиению строк:
            // убираем пустые, но каждая непустая строка => отдельная строчка
            var lines = block.Split(new[] { '#', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                paragraph.Inlines.Add(new Bold(new Run(line)));
                paragraph.Inlines.Add(new LineBreak());
            }

            // Убираем последний LineBreak (если есть)
            if (paragraph.Inlines.LastInline is LineBreak)
            {
                paragraph.Inlines.Remove(paragraph.Inlines.LastInline);
            }

            doc.Blocks.Add(paragraph);
            return doc;
        }

        /// <summary>Создаём FlowDocument с простым текстом (на случай «<ПУСТО>» и т.д.).</summary>
        private FlowDocument CreateDocumentWithText(string text)
        {
            var document = new FlowDocument
            {
                FontFamily = new FontFamily("Arial"),
                IsOptimalParagraphEnabled = true,
                IsHyphenationEnabled = true,
                TextAlignment = TextAlignment.Center
            };

            var paragraph = new Paragraph
            {
                Inlines = { new Run(text) }
            };
            document.Blocks.Add(paragraph);
            return document;
        }

        /// <summary>Удаляем пробелы в конце строки.</summary>
        private string RemoveTrailingSpaces(string line)
        {
            return line.TrimEnd(' ');
        }

        /// <summary>Удаляем «* * *» из конца последнего блока (если есть).</summary>
        private void RemoveEndSymbols()
        {
            if (BlocksCount == 0) return;

            string lastBlock = Blocks[BlocksCount - 1];
            // Удаляем любые * в конце
            var modified = Regex.Replace(lastBlock, @"(\*+\s*)+$", "").TrimEnd();
            Blocks[BlocksCount - 1] = modified;
        }

        /// <summary>Добавляем «* * *» в конец песни, если их там нет.</summary>
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
