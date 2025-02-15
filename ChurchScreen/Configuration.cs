using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChurchScreen
{
    public class Configuration
    {
        public int FontSizeStep;
        public bool AlwaysServiceMode;
        public bool SaveAsk;
        public bool UseOneMonitor;//использовать только первый монитор (для отладки)
        public uint StrechFill;//тип растягивания для фона на основном экране. Если true - тип растягивания UniformToFill иначе Uniform
        public bool UseDefaultBackground;//использовать файл default для фона по умолчанию или нет
        public int FontSizeForSplit;//размер шрифта, при котором рассчетно можно делить блок пополам

        public Configuration()
        {
            FontSizeStep = 5;
            AlwaysServiceMode = false;
            SaveAsk = true;
            UseOneMonitor = false;
            StrechFill = 2;
            UseDefaultBackground = false;
        }
    }
}
