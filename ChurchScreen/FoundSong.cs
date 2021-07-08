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
        private string[] FileList;
        public FoundSong()
        {
            FileList = Directory.GetFiles(Environment.CurrentDirectory + "\\songs\\", "*");
            //Console.WriteLine("The number of files is {0}.", FileList.Length);            
        }

        public List<SearchItem> GetSongFileName(String searchStr)
        {            
            List<SearchItem> result = new List<SearchItem>();
            if (searchStr.Length < 2) return result;
            SearchItem si;            
            string songText;
            String fileData;
            StreamReader s;
            Encoding encoding;


            Task task1 = Task.Factory.StartNew(() =>
            {
                foreach (string dir in FileList)
                {
                    encoding = SongDocument.GetFileEncoding(dir);
                    if (encoding == Encoding.UTF8)
                    {
                        s = File.OpenText(dir);
                        fileData = s.ReadLine();
                        s.Close();
                    }
                    else
                    {
                        byte[] ansiBytes = File.ReadAllBytes(dir);
                        var utf8String = Encoding.Default.GetString(ansiBytes);
                        fileData = utf8String.ToString();
                        fileData = fileData.Substring(0, fileData.IndexOf("\n"));
                    }
                    if (!fileData.Contains("@01"))
                        continue;
                    fileData = fileData.ToLower();
                    searchStr = searchStr.ToLower();
                    if (fileData.Contains(searchStr))
                    {
                        si = new SearchItem();
                        si.SongName = Path.GetFileNameWithoutExtension(dir);
                        SongDocument song = new SongDocument(dir, 0);
                        if (song.coopletList.Count != 0)
                        {
                            songText = song.coopletList[0];
                            songText = songText.Substring(0, songText.IndexOf("\n"));
                            songText = songText.Trim();
                            si.SongText = songText;
                            result.Add(si);
                        }
                    }
                    else
                        continue;
                }
            });
            task1.Wait();
            task1.Dispose();
            return result;
        }
    }
}
