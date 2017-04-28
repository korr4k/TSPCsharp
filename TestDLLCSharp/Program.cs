using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;     // DLL support

namespace TestDLLCSharp
{
    class Program
    {

        [DllImport("TestDLL.dll")]
        public static extern void DisplayHelloFromDLL(char[] test, int i);

        static void Main(string[] args)
        {
            Console.WriteLine("This is C# program");

            char[] test = new char[3];
            test[0] = 'c';
            test[1] = 'c';
            test[2] = 'c';
            DisplayHelloFromDLL(test, 0);
            Console.ReadLine();
        }
    }
}
