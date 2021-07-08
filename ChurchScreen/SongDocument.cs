using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        public bool ServiseMode { set; get; }
        public String FileName { set; get; }        

        public int CurrentBlockNumber
        {
            get
            {
                if (myCurrentBlockNumber > BlocksCount) return BlocksCount;
                else return myCurrentBlockNumber;
            }
        }
        public int BlocksCount { get; set; }
        private int ScreenWidth { get; set; }
        //private List<String> coopletList;
        public List<String> coopletList;//19.05.2017 - заменил модификатор доступа, чтобы иметь доступ из FoundSong
        private List<int> coopletFontSizeList;

        public int BlockFontSize
        {
            get
            {
                if (CurrentBlockNumber != 0)
                    return coopletFontSizeList[CurrentBlockNumber - 1];
                else
                    return 0;
            }
            set
            {
                coopletFontSizeList[CurrentBlockNumber - 1] = value;
            }
        }

        public bool IsEnd
        {
            get
            {
                if (myCurrentBlockNumber > BlocksCount) return true;
                else return false;
            }
        }

        public static Encoding GetFileEncoding(string srcFile)
        {
            // *** Use Default of Encoding.Default (Ansi CodePage)
            Encoding enc = Encoding.Default;

            // *** Detect byte order mark if any - otherwise assume default
            byte[] buffer = new byte[5];
            FileStream file = new FileStream(srcFile, FileMode.Open);
            file.Read(buffer, 0, 5);
            file.Close();

            if (buffer[0] == 0xef && buffer[1] == 0xbb && buffer[2] == 0xbf)
                enc = Encoding.UTF8;
            else if (buffer[0] == 0xfe && buffer[1] == 0xff)
                enc = Encoding.Unicode;
            else if (buffer[0] == 0 && buffer[1] == 0 && buffer[2] == 0xfe && buffer[3] == 0xff)
                enc = Encoding.UTF32;
            else if (buffer[0] == 0x2b && buffer[1] == 0x2f && buffer[2] == 0x76)
                enc = Encoding.UTF7;
            else if (buffer[0] == 0xFE && buffer[1] == 0xFF)
                // 1201 unicodeFFFE Unicode (Big-Endian)
                enc = Encoding.GetEncoding(1201);
            else if (buffer[0] == 0xFF && buffer[1] == 0xFE)
                // 1200 utf-16 Unicode
                enc = Encoding.GetEncoding(1200);


            return enc;
        }

        public SongDocument(String fileName, int screenWidth)
        {
            if (fileName.Trim() == "") return;
            if (fileName.Substring(fileName.Length - 4, 4) != ".txt")
                FileName = fileName + ".txt";
            else
                FileName = fileName;
            if (!File.Exists(FileName)) 
            {
                FileName = Environment.CurrentDirectory + "\\songs\\" + fileName + ".txt";
                if (!File.Exists(FileName))
                    return;
            }            
            myCurrentBlockNumber = 0;//в самом начале номер куплета не определен
            BlocksCount = 0;
            coopletList = new List<String>();
            coopletFontSizeList = new List<int>();

            ScreenWidth = screenWidth;

            

            //Загружаем текст и разбиваем его по блокам:
            string block, search_str;
            String fileData;
            StreamReader s;

            Encoding encoding = GetFileEncoding(FileName);
            if (encoding == Encoding.UTF8)
            {
                s = File.OpenText(FileName);
                StreamReader cooplet = new StreamReader(s.BaseStream);
                fileData = s.ReadToEnd();
                s.Close();
            }
            else
            {
                byte[] ansiBytes = File.ReadAllBytes(FileName);
                var utf8String = Encoding.Default.GetString(ansiBytes);
                fileData = utf8String.ToString();
            }
            
            int i = 0;
            int start_index, length, font;
            String noiseBlock;

            string subBlock = "";
            if (fileData.IndexOf("@01") == -1)
            {//текст в файле разделен на блоки только переводами строки - включаем сервисный режим
                ServiseMode = true;
                fileData += "\n";
                string[] strs = fileData.Split('\n');
                for (int j = 0; j < strs.Length; j++)
                {
                    if (strs[j].Trim() != "")
                    {
                        subBlock += strs[j];
                        subBlock += "\n";
                    }
                    else
                    {
                        if (subBlock.Trim() != "")
                        {
                            coopletList.Add(subBlock);
                            coopletFontSizeList.Add(-1);
                            subBlock = "";
                            BlocksCount++;
                        }
                    }                    
                }
            }
            else
            {//файл в стандартном формате
                ServiseMode = false;
                while (true)
                {                
                    i++;
                    search_str = String.Format("@{0:00}", i);
                    if (fileData.IndexOf(search_str) == -1)
                    {
                        BlocksCount = i - 1;
                        break;
                    }


                    search_str = String.Format("${0:00}", i);

                    start_index = fileData.IndexOf(String.Format("@{0:00}", i)) + 3;
                    length = fileData.IndexOf(search_str) - start_index;
                    if (length < 0) continue;
                    block = fileData.Substring(start_index, length);
               

                

                    /*if (fileData.IndexOf("#") == (start_index - 5))
                        noiseBlock = fileData.Substring(start_index - 5, (fileData.IndexOf(search_str) - (start_index - 5)));
                    else if (fileData.IndexOf("#") == (start_index - 6))
                        noiseBlock = fileData.Substring(start_index - 6, (fileData.IndexOf(search_str) - (start_index - 6)));
                    else if (fileData.IndexOf("#") == (start_index - 7))
                        noiseBlock = fileData.Substring(start_index - 7, (fileData.IndexOf(search_str) - (start_index - 7)));
                    else if ((fileData.IndexOf("#") == (start_index - 8)))
                        noiseBlock = fileData.Substring(start_index - 8, (fileData.IndexOf(search_str) - (start_index - 8)));
                    else noiseBlock = "";

                    search_str = String.Format("@{0:00}", i);

                    if (noiseBlock.Trim() != "")
                    {
                        if ((noiseBlock.IndexOf(search_str) - 1) >= 0)
                        {
                            if ((noiseBlock.IndexOf("#") < noiseBlock.IndexOf(search_str)) && (noiseBlock.IndexOf("#") != -1))
                            {
                                string tmp = noiseBlock.Substring(noiseBlock.IndexOf("#") + 1, (((start_index - 3) - noiseBlock.IndexOf("#")) - 1));
                                font = Convert.ToInt32(tmp);
                                coopletFontSizeList.Add(font);
                            }
                            else coopletFontSizeList.Add(-1);
                        }
                    }
                    else coopletFontSizeList.Add(-1);

                    string tmp2 = String.Format("@{0:00}", i + 1);

                    if(fileData.IndexOf(tmp2) != -1)
                        fileData = fileData.Substring(fileData.IndexOf(tmp2), fileData.Length - fileData.IndexOf(tmp2));*/

                    if (block.IndexOf("#") != -1)
                    {
                        font = Convert.ToInt32(block.Substring(block.IndexOf("#") + 1, 3));
                        if (font > 0 && font < 999)
                            coopletFontSizeList.Add(font);
                        else
                            coopletFontSizeList.Add(-1);
                    }
                    else coopletFontSizeList.Add(-1);

                    block = block.Substring(4, block.Length - 4);
                    coopletList.Add(block);
                }
            }            

            //что-то не так...
            if (coopletList.Count != coopletFontSizeList.Count)
            {
                myCurrentBlockNumber = 0;//в самом начале номер куплета не определен
                BlocksCount = 0;
                coopletList.Clear();
                coopletFontSizeList.Clear();
            }

        }

        public bool SaveSong()
        {
            StreamWriter s = new StreamWriter(FileName, false, Encoding.UTF8);            

            string str;

            try
            {
                for (int i = 0; i < coopletList.Count; i++)
                {
                    str = String.Format("@{0:00}", i + 1);

                    if (coopletFontSizeList[i] != -1)
                        str += String.Format("#{0:000}", coopletFontSizeList[i]);
                    else
                        str += String.Format("#{0:000}", CalculateFont(coopletList[i]));
                        
                    str += coopletList[i];
                    str += String.Format("${0:00}", i + 1);

                    s.Write(str);
                }
            }
            catch (Exception e)
            {
                return false;
            }

            s.Close();
            

            return true;
        }

        public void InsertRefrain()
        {
            //if (BlocksCount == 0)
            if (BlocksCount < 2)
                return;
            string pripev = coopletList[1];
            List<String> tmpBlocks = new List<String>();
            List<int> tmpBlocksFonts = new List<int>();

            //удаляем припев из исходного списка:
            coopletList.RemoveAt(1);
            coopletFontSizeList.RemoveAt(1);

            int j = 0;
            for (int i = 0; i < coopletList.Count * 2; i++)
            {
                if (i % 2 == 0)
                {
                    tmpBlocks.Add(coopletList[j]);
                    tmpBlocksFonts.Add(coopletFontSizeList[j]);
                    j++;
                }
                else
                {
                    tmpBlocks.Add(pripev);
                    tmpBlocksFonts.Add(-1);
                }
            }
            BlocksCount = tmpBlocks.Count;

            coopletList = tmpBlocks;
            coopletFontSizeList = tmpBlocksFonts;
        }

        private FlowDocument getDocument(int number, bool getPreview)
        {         
            FlowDocument document = new FlowDocument();
            document.FontFamily = new FontFamily("Arial");

            if (coopletList.Count == 0) return document;

            //песня закончилась
            if (number > BlocksCount) return document;


            //Блок ниже(основная его часть) написан, как функция CalculateFont, но я оставил дублируюшийся код от греха подальше :)
            //**********************************************************************************************************************
            String block;
            block = coopletList[number - 1];
            string[] phrases = block.Split('#', '\r', '\n');

            int previewFontSize = 0, mainFontSizeNormal = 0, mainFontSizeBold = 0;
            //ищем максимальную по размеру строку, чтобы посчитать по пропорции размер шрифта, который подойдет
            int maxLength = 0;
            string maxLengthStr = "";
            foreach (string str in phrases)
            {                    
                if (str.Length > maxLength)
                {
                    maxLength = str.Length;                    
                    maxLengthStr = str;
                }
            }

            //для расчета размера шрифта учитываем большие буквы
            double strLength = maxLength;

            for (int i = 0; i < maxLength; i++)
            {
                if(System.Char.IsUpper(maxLengthStr[i]))
                {
                    if(maxLengthStr[i] == 'Ш' || maxLengthStr[i] == 'Щ' || maxLengthStr[i] == 'Ф' || maxLengthStr[i] == 'Ж' 
                        || maxLengthStr[i] == 'Ю' || maxLengthStr[i] == 'Ы' || maxLengthStr[i] == 'М')
                    {
                        strLength += 1;
                    }
                    else
                    {
                        strLength += 0.5;
                    }
                }
            }

            strLength += 0.5;
            maxLength = Convert.ToInt32(strLength);
            previewFontSize = 12 * 54 / maxLength;

            if (coopletFontSizeList[number - 1] == -1)
            {
                int symbCountNormal = ScreenWidth * 300 / 1920;
                int symbCountBold = ScreenWidth * 280 / 1920;

                mainFontSizeNormal = 12 * symbCountNormal / maxLength;
                mainFontSizeBold = 12 * symbCountBold / maxLength;
            }
            else
            {
                mainFontSizeNormal = coopletFontSizeList[number - 1];
                mainFontSizeBold = coopletFontSizeList[number - 1];
            }
            coopletFontSizeList[number - 1] = mainFontSizeBold;
            //**********************************************************************************************************************
            

            Paragraph paragraph = new Paragraph();

            int index = 0;
            int index_of_last_str = 0;//индекс последней строки в массиве со значимым текстом: после неё не будем добавлять пробел, чтобы блок текста был четко центрован по вертикали

            foreach (string str in phrases)
            {
                index++;
                if (str.Trim().Length == 0)
                    continue;
                else
                    index_of_last_str = index;
            }            

            
            index = 0;
            foreach (string str in phrases)
            {
                index++;
                if (str.Trim().Length == 0)
                    continue;

                Bold b = new Bold();                
                b.Inlines.Add(str);
                if (index != index_of_last_str)
                    b.Inlines.Add("\n");//после последней строки не добавляем \n
                
                paragraph.Inlines.Add(b);

            }

            if (!getPreview)
            {
                paragraph.FontSize = mainFontSizeBold;
                //document.FontSize = mainFontSizeBold;
            }
            else
                paragraph.FontSize = previewFontSize;

            document.IsOptimalParagraphEnabled = true;
            document.IsHyphenationEnabled = true;
            document.TextAlignment = TextAlignment.Center;
            document.Blocks.Add(paragraph);

            return document;
        }

        public int CalculateFont()
        {
            return CalculateFont(coopletList[CurrentBlockNumber - 1]);            
        }

        private int CalculateFont(string block)
        {
            if (block.Trim() == "") return 90;
            
            string[] phrases = block.Split('#', '\r', '\n');

            int mainFontSize = 0;
            //ищем максимальную по размеру строку, чтобы посчитать по пропорции размер шрифта, который подойдет
            int maxLength = 0;
            string maxLengthStr = "";
            foreach (string str in phrases)
            {
                if (str.Length > maxLength)
                {
                    maxLength = str.Length;
                    maxLengthStr = str;
                }
            }

            //для расчета размера шрифта учитываем большие буквы
            double strLength = maxLength;

            for (int i = 0; i < maxLength; i++)
            {
                if (System.Char.IsUpper(maxLengthStr[i]))
                {
                    if (maxLengthStr[i] == 'Ш' || maxLengthStr[i] == 'Щ' || maxLengthStr[i] == 'Ф' || maxLengthStr[i] == 'Ж'
                        || maxLengthStr[i] == 'Ю' || maxLengthStr[i] == 'Ы' || maxLengthStr[i] == 'М')
                    {
                        strLength += 1;
                    }
                    else
                    {
                        strLength += 0.5;
                    }
                }
            }
            strLength += 0.5;

            maxLength = Convert.ToInt32(strLength);

            int symbCountBold = ScreenWidth * 280 / 1920;
            mainFontSize = 12 * symbCountBold / maxLength;

            return mainFontSize;
        }

        public FlowDocument NextBlock()
        {
            FlowDocument doc = new FlowDocument();
            if (BlocksCount == 0) return doc;

            if (myCurrentBlockNumber >= BlocksCount)
            {
                //последний номер - на 1 больше, чем кол-во блоков, чтобы не показывать последний на майн
                if (myCurrentBlockNumber < BlocksCount + 1)
                {
                    myCurrentBlockNumber++;
                }
                Paragraph paragraph = new Paragraph();
                Run r = new Run();
                r.Text = "<КОНЕЦ>";
                paragraph.Inlines.Add(r);
                paragraph.TextAlignment = TextAlignment.Center;
                doc.Blocks.Add(paragraph);
                return doc;
                
            }
            else
            {
                myCurrentBlockNumber++;
                doc = getDocument(myCurrentBlockNumber, true);                
                return doc;
            }
        }

        public FlowDocument PreviousBlock()
        {
            FlowDocument doc = new FlowDocument();
            if (BlocksCount == 0) return doc;

            if (CurrentBlockNumber == 0) myCurrentBlockNumber = 1;

            if (1 == CurrentBlockNumber)
            {
                if (myCurrentBlockNumber == 2) myCurrentBlockNumber--;
                doc = getDocument(1, true);
                return doc;
            }
            else
            {
                myCurrentBlockNumber--;
                doc = getDocument(CurrentBlockNumber, true);                
                return doc;
            }
        }

        public FlowDocument ToMainScreen()
        {
            FlowDocument doc = new FlowDocument();
            if (BlocksCount == 0) return doc;
            
            doc = getDocument(CurrentBlockNumber, false);
            return doc;            
        }

        public static FlowDocument cleanDocument()
        {
            FlowDocument doc = new FlowDocument();
            return doc;
        }

    }
}
