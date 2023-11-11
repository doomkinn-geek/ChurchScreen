using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ChurchScreen
{
    public class SongDocument
    {
        private int myCurrentBlockNumber;
        public bool ServiseMode { get; set; }
        public string FileName { get; set; }
        public int BlocksCount
        {
            get
            {
                if (coopletList == null)
                {
                    return 0;
                }
                return coopletList.Count;
            }
        }
        private int ScreenWidth { get; set; }
        private int FontSizeForSplit { get; set; }
        public List<string> coopletList { get; private set; }
        private List<int> coopletFontSizeList;

        public int CurrentBlockNumber => Math.Min(myCurrentBlockNumber, BlocksCount);

        public int BlockFontSize
        {
            get => CurrentBlockNumber != 0 ? coopletFontSizeList[CurrentBlockNumber - 1] : 0;
            set => coopletFontSizeList[CurrentBlockNumber - 1] = value;
        }

        public bool IsEnd => myCurrentBlockNumber > BlocksCount;

        private Dictionary<char, double> widthCoefficients = new Dictionary<char, double>
        {
            // Латинские символы
            { 'W', 1.5 }, { 'M', 1.4 }, { 'm', 1.3 }, { 'w', 1.3 },
            { 'i', 0.7 }, { 'l', 0.6 }, { 'j', 0.6 }, { 't', 0.8 }, { 'f', 0.8 }, { 'r', 0.9 },

            // Кириллические символы
            { 'Ш', 1.4 }, { 'М', 1.4 }, { 'м', 1.3 }, { 'ш', 1.3 }, { 'щ', 1.5 }, { 'ф', 1.3 },
            { 'й', 0.8 }, { 'л', 0.9 }, { 'т', 0.9 }, { 'и', 0.9 },

            // Цифры и знаки препинания
            { '1', 0.8 }, { '.', 0.6 }, { ',', 0.6 }, { ':', 0.6 }, { ';', 0.6 }, { '!', 0.7 },

            // ... добавьте другие символы по мере необходимости
        };


        public SongDocument(string fileName, int screenWidth, int fontSizeForSplit)
        {
            Initialize(fileName, screenWidth);
            FontSizeForSplit = fontSizeForSplit;
        }

        private void Initialize(string fileName, int screenWidth)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return;

            FileName = fileName.EndsWith(".txt") ? fileName : fileName + ".txt";

            if (!File.Exists(FileName))
            {
                FileName = Path.Combine(Environment.CurrentDirectory, "songs", FileName);
                if (!File.Exists(FileName)) return;
            }

            myCurrentBlockNumber = 0;            
            coopletList = new List<string>();
            coopletFontSizeList = new List<int>();
            ScreenWidth = screenWidth;

            string fileData;
            Encoding encoding = GetFileEncoding(FileName);
            if (encoding == Encoding.UTF8)
            {
                fileData = File.ReadAllText(FileName, Encoding.UTF8);
            }
            else
            {
                byte[] ansiBytes = File.ReadAllBytes(FileName);
                fileData = Encoding.Default.GetString(ansiBytes);
            }

            // Определение режима на основе содержимого файла
            ServiseMode = !fileData.Contains("@01");

            if (ServiseMode)
            {
                LoadTextByEmptyLines(fileData);
            }
            else
            {
                LoadTextAndSplitIntoBlocks(fileData);
            }

            AddEndSymbols(); // Добавить символы "*****" после инициализации и загрузки файла
        }

        private void LoadTextAndSplitIntoBlocks(string fileData)
        {            
            if (string.IsNullOrEmpty(fileData))
            {
                // Если файл пуст или его содержимое не может быть прочитано, выходим из функции.
                return;
            }

            // Регулярное выражение для поиска блоков текста в формате @<номер блока>#<размер шрифта для блока><текст блока>$<номер блока>
            var blockPattern = @"@\d{2}#(\d{3})(.*?)\$\d{2}";
            var matches = Regex.Matches(fileData, blockPattern, RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                var block = match.Groups[2].Value.Trim();
                coopletList.Add(block);

                // Извлечение размера шрифта из блока.
                if (int.TryParse(match.Groups[1].Value, out int fontSize))
                {
                    coopletFontSizeList.Add(fontSize);
                }
                else
                {
                    coopletFontSizeList.Add(-1); // Значение по умолчанию для размера шрифта.
                }
            }

            // Проверка на соответствие количества блоков и размеров шрифта.
            if (coopletList.Count != coopletFontSizeList.Count)
            {
                ResetSongData();
            }
        }

        private void LoadTextByEmptyLines(string fileData)
        {
            // Разбиваем текст на блоки по двум новым строкам.
            string[] blocks = fileData.Split(new string[] { "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var rawBlock in blocks)
            {
                var block = RemoveTrailingSpaces(rawBlock.Trim());
                List<int> splitFontSizes;
                var splitBlocks = SplitBlockIfNecessary(block, out splitFontSizes);

                coopletList.AddRange(splitBlocks);
                coopletFontSizeList.AddRange(splitFontSizes);
            }

            // Проверка на соответствие количества блоков и размеров шрифта.
            if (coopletList.Count != coopletFontSizeList.Count)
            {
                ResetSongData();
            }
        }

        private string ReadFileContent(string fileName)
        {
            try
            {
                Encoding encoding = GetFileEncoding(fileName);
                if (encoding == Encoding.UTF8)
                {
                    return File.ReadAllText(fileName, Encoding.UTF8);
                }
                else
                {
                    byte[] ansiBytes = File.ReadAllBytes(fileName);
                    return Encoding.Default.GetString(ansiBytes);
                }
            }
            catch (Exception ex)
            {
                // Здесь можно добавить логирование ошибки, если это необходимо.
                return string.Empty;
            }
        }


        private void ResetSongData()
        {
            myCurrentBlockNumber = 0;
            coopletList.Clear();
            coopletFontSizeList.Clear();
        }


        public static Encoding GetFileEncoding(string srcFile)
        {
            using (var file = new FileStream(srcFile, FileMode.Open))
            {
                var buffer = new byte[5];
                file.Read(buffer, 0, 5);

                if (buffer[0] == 0xef && buffer[1] == 0xbb && buffer[2] == 0xbf) return Encoding.UTF8;
                if (buffer[0] == 0xfe && buffer[1] == 0xff) return Encoding.Unicode;
                if (buffer[0] == 0 && buffer[1] == 0 && buffer[2] == 0xfe && buffer[3] == 0xff) return Encoding.UTF32;
                if (buffer[0] == 0x2b && buffer[1] == 0x2f && buffer[2] == 0x76) return Encoding.UTF7;
                if (buffer[0] == 0xFE && buffer[1] == 0xFF) return Encoding.GetEncoding(1201); // Unicode (Big-Endian)
                if (buffer[0] == 0xFF && buffer[1] == 0xFE) return Encoding.GetEncoding(1200); // utf-16 Unicode

                return Encoding.Default;
            }
        }

        public bool SaveSong()
        {
            try
            {
                using (var writer = new StreamWriter(FileName, false, Encoding.UTF8))
                {
                    for (int i = 0; i < coopletList.Count; i++)
                    {
                        string str = String.Format("@{0:00}", i + 1);

                        // Если размер шрифта для блока определен, используем его. В противном случае рассчитываем размер шрифта.
                        if (coopletFontSizeList[i] != -1)
                            str += String.Format("#{0:000}", coopletFontSizeList[i]);
                        else
                            str += String.Format("#{0:000}", CalculateFont(coopletList[i]));

                        str += coopletList[i];
                        str += String.Format("${0:00}", i + 1);

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

            // Удалить символы "*****" из последнего блока перед вставкой припева
            RemoveEndSymbols();

            // Определение блоков припева
            List<string> refrainBlocks = new List<string>();
            if (coopletList[0].EndsWith(" =>"))
            {
                refrainBlocks.Add(coopletList[2]);
                refrainBlocks.Add(coopletList[3]);
            }
            else
            {
                refrainBlocks.Add(coopletList[1]);
            }

            var updatedBlocks = new List<string>();
            var updatedFonts = new List<int>();

            for (int i = 0; i < coopletList.Count; i++)
            {
                updatedBlocks.Add(coopletList[i]);
                updatedFonts.Add(coopletFontSizeList[i]);

                // Если текущий блок заканчивается на " =>", это означает, что это первая часть разбитого куплета.
                // В этом случае мы пропускаем добавление припева после первой части куплета.
                if (coopletList[i].EndsWith(" =>"))
                {
                    continue;
                }

                // Если текущий блок не является припевом и следующий блок также не является припевом (или его нет), добавить припев.
                if (!refrainBlocks.Contains(coopletList[i]) && (i + 1 == coopletList.Count || !refrainBlocks.Contains(coopletList[i + 1])))
                {
                    foreach (var refrainBlock in refrainBlocks)
                    {
                        updatedBlocks.Add(refrainBlock);
                        updatedFonts.Add(-1);
                    }
                }
            }

            coopletList = updatedBlocks;
            coopletFontSizeList = updatedFonts;

            // Добавить символы "*****" обратно в конец песни после вставки припева
            AddEndSymbols();
        }



        private FlowDocument GetDocument(int number, bool getPreview)
        {
            var document = new FlowDocument
            {
                FontFamily = new FontFamily("Arial"),
                IsOptimalParagraphEnabled = true,
                IsHyphenationEnabled = true,
                TextAlignment = TextAlignment.Center
            };

            if (number <= 0 || number > coopletList.Count) return document;

            var block = coopletList[number - 1];
            var fontSize = getPreview ? CalculatePreviewFontSize(block) : coopletFontSizeList[number - 1];

            // Проверка на допустимость значения размера шрифта
            if (fontSize <= 0)
            {
                fontSize = CalculateFont(block); // Рассчитываем размер шрифта, если текущее значение не допустимо
            }

            var paragraph = new Paragraph { FontSize = fontSize };
            foreach (var line in block.Split(new[] { '#', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                paragraph.Inlines.Add(new Bold(new Run(line)));
                paragraph.Inlines.Add(new LineBreak());
            }

            document.Blocks.Add(paragraph);
            return document;
        }

        public int CalculatePreviewFontSize(string block)
        {
            var mainFontSize = CalculateFont(block);
            double scaleFactor = 320.0 / ScreenWidth; // Масштабирование на основе ширины экрана
            return (int)(mainFontSize * scaleFactor);
        }


        public int CalculateFont()
        {
            return CalculateFont(coopletList[CurrentBlockNumber - 1]);
        }

        private int CalculateFont(string block)
        {
            if (string.IsNullOrWhiteSpace(block)) return 90;

            var lines = block.Split(new[] { '#', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var maxLengthStr = lines.OrderByDescending(s => s.Length).FirstOrDefault() ?? string.Empty;

            double weightedLength = 0;
            foreach (char c in maxLengthStr)
            {
                if (widthCoefficients.ContainsKey(c))
                    weightedLength += widthCoefficients[c];
                else
                    weightedLength += 1; // стандартный коэффициент для символов, которые не в словаре
            }

            var symbCountBold = ScreenWidth * 280 / 1920;
            var fontSizeByWidth = (int)(12 * symbCountBold / weightedLength);

            // Высота экрана для соотношения сторон 16:9
            var screenHeight = ScreenWidth * 9 / 16;

            // Максимальное количество строк, которое может поместиться на экране с учетом расчетного размера шрифта
            var maxLinesOnScreen = screenHeight / (fontSizeByWidth * 1.5); // 1.5 - это коэффициент, учитывающий интервал между строками

            // Если количество строк в блоке превышает максимальное количество строк на экране, корректируем размер шрифта
            if (lines.Length > maxLinesOnScreen)
            {
                fontSizeByWidth = (int)(fontSizeByWidth * maxLinesOnScreen / lines.Length);
            }

            return fontSizeByWidth;
        }

        public FlowDocument FirstBlock()
        {
            // Устанавливаем текущий номер блока на 1 (первый блок).
            myCurrentBlockNumber = 1;

            // Если нет блоков, возвращаем документ с текстом "<ПУСТО>".
            if (BlocksCount == 0) return CreateDocumentWithText("<ПУСТО>");

            // Возвращаем документ для первого блока.
            return GetDocument(myCurrentBlockNumber, true);
        }

        public FlowDocument CurrentBlock()
        {
            // Если нет блоков, возвращаем документ с текстом "<ПУСТО>".
            if (BlocksCount == 0) return CreateDocumentWithText("<ПУСТО>");

            // Возвращаем документ для первого блока.
            return GetDocument(CurrentBlockNumber, true);
        }


        public FlowDocument NextBlock()
        {
            if (BlocksCount == 0) return CreateDocumentWithText("<ПУСТО>");

            if (myCurrentBlockNumber >= BlocksCount)
            {
                myCurrentBlockNumber = Math.Min(myCurrentBlockNumber + 1, BlocksCount + 1);
                return CreateDocumentWithText("<КОНЕЦ>");
            }

            return GetDocument(++myCurrentBlockNumber, true);
        }

        public FlowDocument PreviousBlock()
        {
            if (BlocksCount == 0) return CreateDocumentWithText("<ПУСТО>");

            if (myCurrentBlockNumber <= 1)
            {
                myCurrentBlockNumber = Math.Max(1, myCurrentBlockNumber - 1);
                return GetDocument(1, true);
            }

            return GetDocument(--myCurrentBlockNumber, true);
        }

        public FlowDocument ToMainScreen()
        {
            if (BlocksCount == 0) return CreateDocumentWithText("<ПУСТО>");

            return GetDocument(myCurrentBlockNumber, false);
        }

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


        public static FlowDocument CleanDocument()
        {
            return new FlowDocument();
        }

        private string RemoveTrailingSpaces(string line)
        {
            return line.TrimEnd(' ');
        }

        
        public void UndoSplitBlocks()
        {
            RemoveEndSymbols();
            var restoredBlocks = new List<string>();
            var restoredFontSizes = new List<int>();

            for (int i = 0; i < coopletList.Count; i++)
            {
                if (coopletList[i].EndsWith(" =>") && i + 1 < coopletList.Count)
                {
                    // Объединяем два блока, удаляя " =>" из первой части
                    var combinedBlock = coopletList[i].TrimEnd(' ', '=', '>') + Environment.NewLine + coopletList[i + 1];
                    restoredBlocks.Add(combinedBlock);

                    // Пересчитываем размер шрифта для объединенного блока
                    restoredFontSizes.Add(CalculateFont(combinedBlock));

                    i++; // Пропускаем следующий блок, так как он уже был объединен
                }
                else
                {
                    restoredBlocks.Add(coopletList[i]);
                    restoredFontSizes.Add(coopletFontSizeList[i]);
                }
            }           

            coopletList = restoredBlocks;
            coopletFontSizeList = restoredFontSizes;
            AddEndSymbols();
        }

        public void UndoSplitForBlock(int blockNumber)
        {
            if (blockNumber <= 0 || blockNumber > coopletList.Count)
                return;

            bool isRefrain = IsRefrainBlock(blockNumber);

            // Если блок заканчивается на " =>", это первая часть разбитого блока
            if (coopletList[blockNumber - 1].EndsWith(" =>"))
            {
                // Объединяем два блока, удаляя " =>" из первой части
                var combinedBlock = coopletList[blockNumber - 1].TrimEnd(' ', '=', '>') + Environment.NewLine + coopletList[blockNumber];
                coopletList[blockNumber - 1] = combinedBlock;

                // Удаляем следующий блок, так как он уже был объединен
                coopletList.RemoveAt(blockNumber);
                coopletFontSizeList.RemoveAt(blockNumber);
            }

            // Если блок является припевом, перестраиваем все блоки, которые являются припевами
            if (isRefrain)
            {
                RestoreAllRefrains();
            }
        }

        private void RestoreAllRefrains()
        {
            List<int> refrainIndices = new List<int>();
            bool hasEndSymbols = coopletList.Last().EndsWith("* * *");

            // Если последний блок содержит "***", убираем его для корректного сравнения
            if (hasEndSymbols)
            {
                coopletList[coopletList.Count - 1] = coopletList.Last().TrimEnd('*', ' ');
            }

            for (int i = 0; i < coopletList.Count; i++)
            {
                if (IsRefrainBlock(i + 1)) // +1 потому что блоки индексируются с 1
                {
                    if (coopletList[i].EndsWith(" =>"))
                    {
                        refrainIndices.Add(i);
                    }
                }
            }

            // Восстанавливаем блоки в обратном порядке
            for (int i = refrainIndices.Count - 1; i >= 0; i--)
            {
                int index = refrainIndices[i];
                var combinedBlock = coopletList[index].TrimEnd(' ', '=', '>') + Environment.NewLine + coopletList[index + 1];
                coopletList[index] = combinedBlock;

                coopletList.RemoveAt(index + 1);
                coopletFontSizeList.RemoveAt(index + 1);
            }

            // Если в конце были символы "***", добавляем их обратно
            if (hasEndSymbols)
            {
                coopletList[coopletList.Count - 1] += "* * *";
            }
        }




        private bool IsRefrainBlock(int blockNumber)
        {
            if (blockNumber <= 0 || blockNumber > coopletList.Count)
                return false;

            string block = coopletList[blockNumber - 1];
            int occurrences = coopletList.Count(b => b == block);

            // Если блок встречается более одного раза и не является частью разбитого блока
            if (occurrences > 1 && !block.EndsWith(" =>") && (blockNumber == 1 || !coopletList[blockNumber - 2].EndsWith(" =>")))
            {
                return true;
            }

            // Если текущий блок является первой частью разбитого блока и следующий блок также встречается более одного раза
            if (block.EndsWith(" =>") && blockNumber < coopletList.Count)
            {
                string nextBlock = coopletList[blockNumber];
                int nextOccurrences = coopletList.Count(b => b == nextBlock);
                if (nextOccurrences > 1)
                {
                    return true;
                }
            }

            return false;
        }



        private List<string> SplitBlockIfNecessary(string block, out List<int> fontSizes)
        {
            var result = new List<string>();
            fontSizes = new List<int>();

            if (CalculateFont(block) < FontSizeForSplit)
            {
                var lines = block.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();

                // Разбиваем слишком длинные строки
                for (int i = 0; i < lines.Count; i++)
                {
                    if (CalculateFont(lines[i]) < FontSizeForSplit)
                    {
                        int splitPoint = lines[i].Length / 2;

                        // Ищем ближайший пробел для разделения строки
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

                int mid = lines.Count / 2;
                var firstPart = string.Join(Environment.NewLine, lines.Take(mid)) + " " + " =>";
                var secondPart = string.Join(Environment.NewLine, lines.Skip(mid));

                result.Add(firstPart);
                result.Add(secondPart);

                fontSizes.Add(CalculateFont(firstPart));
                fontSizes.Add(CalculateFont(secondPart));
            }
            else
            {
                result.Add(block);
                fontSizes.Add(CalculateFont(block));
            }

            return result;
        }

        public void SplitLargeBlocksIfNeeded()
        {
            RemoveEndSymbols(); // Удалить символы "***" перед разбиением
            // Проверка, были ли блоки уже разбиты
            if (coopletList.Any(s => s.EndsWith(" =>")))
            {
                return; // Если блоки уже были разбиты, выходим из метода
            }

            var newBlocks = new List<string>();
            var newFontSizes = new List<int>();

            foreach (var block in coopletList)
            {
                if (CalculateFont(block) < FontSizeForSplit)
                {
                    List<int> splitFontSizes;
                    var splitBlocks = SplitBlockIfNecessary(block, out splitFontSizes);

                    newBlocks.AddRange(splitBlocks);
                    newFontSizes.AddRange(splitFontSizes);
                }
                else
                {
                    newBlocks.Add(block);
                    newFontSizes.Add(CalculateFont(block));
                }
            }

            coopletList = newBlocks;
            coopletFontSizeList = newFontSizes;
            AddEndSymbols(); // Добавить символы "*****" после разбиения
        }

        private void RemoveEndSymbols()
        {
            if (coopletList.Count == 0) return;

            // Получить последний блок
            var lastBlock = coopletList.Last();

            // Проверить, содержит ли последний блок символы "*****" в конце
            if (lastBlock.EndsWith("* * *"))
            {
                // Удалить символы "*****" из последнего блока
                coopletList[coopletList.Count - 1] = lastBlock.TrimEnd('*', ' ');
            }
        }


        private void AddEndSymbols()
        {
            if (coopletList.Count == 0) return;

            // Получить последний блок
            var lastBlock = coopletList.Last();

            // Проверить, содержит ли последний блок символы "*****" в конце
            if (!lastBlock.EndsWith("* * *"))
            {
                // Добавить символы "*****" к последнему блоку
                coopletList[coopletList.Count - 1] = lastBlock + Environment.NewLine + "* * *";
            }
        }
        
    }
}
