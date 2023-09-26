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


        public SongDocument(string fileName, int screenWidth)
        {
            Initialize(fileName, screenWidth);
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
                var block = rawBlock.Trim();

                // Добавляем блок в список.
                coopletList.Add(block);

                // Рассчитываем размер шрифта для блока и добавляем его в список.
                coopletFontSizeList.Add(CalculateFont(block));
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


        private string FormatBlockNumber(int number) => $"@{number:00}$";
        private string FormatFontSize(string block) => $"#{CalculateFont(block):000}";
               

        public void InsertRefrain()
        {
            if (BlocksCount < 2) return;

            var refrain = coopletList[1];
            var updatedBlocks = new List<string>();
            var updatedFonts = new List<int>();

            foreach (var block in coopletList)
            {
                updatedBlocks.Add(block);
                updatedBlocks.Add(refrain);
            }

            foreach (var fontSize in coopletFontSizeList)
            {
                updatedFonts.Add(fontSize);
                updatedFonts.Add(-1);
            }

            coopletList = updatedBlocks;
            coopletFontSizeList = updatedFonts;
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

            var paragraph = new Paragraph { FontSize = fontSize };
            foreach (var line in block.Split(new[] { '#', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                paragraph.Inlines.Add(new Bold(new Run(line)));
                paragraph.Inlines.Add(new LineBreak());
            }

            document.Blocks.Add(paragraph);
            return document;
        }

        private int CalculateFont(string block)
        {
            if (string.IsNullOrWhiteSpace(block)) return 90;

            var maxLengthStr = block.Split(new[] { '#', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).OrderByDescending(s => s.Length).FirstOrDefault() ?? string.Empty;

            double weightedLength = 0;
            foreach (char c in maxLengthStr)
            {
                if (widthCoefficients.ContainsKey(c))
                    weightedLength += widthCoefficients[c];
                else
                    weightedLength += 1; // стандартный коэффициент для символов, которые не в словаре
            }

            var symbCountBold = ScreenWidth * 280 / 1920;

            return (int)(12 * symbCountBold / weightedLength);
        }

        private int CalculatePreviewFontSize(string block)
        {
            var maxLengthStr = block.Split(new[] { '#', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).OrderByDescending(s => s.Length).FirstOrDefault() ?? string.Empty;

            var strLength = maxLengthStr.Length + maxLengthStr.Count(char.IsUpper) * 0.5;
            return 12 * 54 / Convert.ToInt32(strLength);
        }


        public int CalculateFont()
        {
            return CalculateFont(coopletList[CurrentBlockNumber - 1]);
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
    }
}
