using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChurchScreen
{
    class PicturesFileName
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public PicturesFileName(string fileName)
        {
            FileName = fileName;
        }
    }
}
