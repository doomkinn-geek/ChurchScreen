using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChurchScreen
{
    public class FoundSong
    {
        private List<string> FileList;

        public FoundSong()
        {
            FileList = Directory.GetFiles(Environment.CurrentDirectory + "\\songs\\", "*").ToList();
        }

        /// <summary>
        /// Ищет песни, в которых (в первой строке) содержится искомая подстрока (без знаков пунктуации).
        /// Возвращает список SearchItem.
        /// </summary>
        public List<SearchItem> GetSongFileName(string searchStr)
        {
            if (string.IsNullOrWhiteSpace(searchStr) || searchStr.Length < 2)
                return new List<SearchItem>();

            searchStr = RemovePunctuation(searchStr.ToLower());

            List<SearchItem> result = new List<SearchItem>();

            Parallel.ForEach(FileList, dir =>
            {
                var fileData = ReadFirstLineFromFile(dir);
                if (string.IsNullOrEmpty(fileData)) return;

                if (!fileData.Contains("@01"))
                    return;

                var cleanedFileData = RemovePunctuation(fileData.ToLower());
                if (cleanedFileData.Contains(searchStr))
                {
                    var si = new SearchItem
                    {
                        SongName = Path.GetFileNameWithoutExtension(dir),
                        SongText = GetSongText(dir)
                    };
                    if (!string.IsNullOrEmpty(si.SongText))
                    {
                        lock (result)
                        {
                            result.Add(si);
                        }
                    }
                }
            });

            return result;
        }

        private string ReadFirstLineFromFile(string filePath)
        {
            Encoding encoding = SongDocument.GetFileEncoding(filePath);
            using (var reader = new StreamReader(filePath, encoding))
            {
                return reader.ReadLine();
            }
        }

        private string GetSongText(string filePath)
        {
            var song = new SongDocument(filePath, 0, 1080, 200);
            if (song.Blocks != null && song.Blocks.Count > 0)
            {
                var firstBlock = song.Blocks[0];
                // Берём первую строку
                var idx = firstBlock.IndexOf("\n", StringComparison.Ordinal);
                if (idx > 0)
                    return firstBlock.Substring(0, idx).Trim();

                return firstBlock.Trim();
            }
            return null;
        }

        private string RemovePunctuation(string input)
        {
            return new string(input.Where(c => !char.IsPunctuation(c)).ToArray());
        }
    }
}
