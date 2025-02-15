using System;

namespace ChurchScreen
{
    public class Configuration
    {
        public int FontSizeStep;         // Шаг изменения шрифта по кнопкам +/-
        public bool AlwaysServiceMode;   // Всегда ли включен сервисный режим
        public bool SaveAsk;             // Спрашивать ли подтверждение при сохранении
        public bool UseOneMonitor;       // Использовать только первый монитор (для отладки)
        public uint StrechFill;          // Тип растягивания фона (0=Fill,1=Uniform,2=UniformToFill)
        public bool UseDefaultBackground;// Использовать файл default...?
        public int FontSizeForSplit;     // Пороговый размер шрифта для разбивки больших блоков

        public Configuration()
        {
            FontSizeStep = 5;
            AlwaysServiceMode = false;
            SaveAsk = true;
            UseOneMonitor = false;
            StrechFill = 2;
            UseDefaultBackground = false;
            FontSizeForSplit = 200; // Примерное значение
        }
    }
}
