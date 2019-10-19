using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace COM_BY_TCP.Comm
{
    class Utility
    {
    }

    public class Log
    {
        public static void Output(string format, params object[] arg)
        {
#if DEBUG
            Console.Write(format, arg);
#endif
        }

        public static void OutputLine(string format, params object[] arg)
        {
#if DEBUG
            Console.WriteLine(format, arg);
#endif
        }
    }
}
