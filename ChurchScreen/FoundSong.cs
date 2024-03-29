﻿using System;
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

        public List<SearchItem> GetSongFileName(string searchStr)
        {
            if (string.IsNullOrWhiteSpace(searchStr) || searchStr.Length < 2)
                return new List<SearchItem>();

            searchStr = RemovePunctuation(searchStr.ToLower());

            List<SearchItem> result = new List<SearchItem>();

            Parallel.ForEach(FileList, dir =>
            {
                var fileData = ReadFirstLineFromFile(dir);

                // Проверка на null
                if (string.IsNullOrEmpty(fileData))
                    return;

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

                    // Дополнительная проверка на null
                    if (!string.IsNullOrEmpty(si.SongText))
                    {
                        result.Add(si);
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
            var song = new SongDocument(filePath, 0, 200);
            if (song.coopletList.Count > 0)
            {
                var songText = song.coopletList[0];
                return songText.Substring(0, songText.IndexOf("\n")).Trim();
            }
            return null;
        }

        private string RemovePunctuation(string input)
        {
            return new string(input.Where(c => !char.IsPunctuation(c)).ToArray());
        }
    }
}
