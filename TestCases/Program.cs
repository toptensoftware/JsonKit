using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TestCases
{
    class Program
    {
        int field { get; set; }

        static void Main(string[] args)
        {
            var p = new Program();
            p.field = 23;

            var str = p.field.ToString();

            //PetaJson.JsonEmit.Init();
            PetaTest.Runner.RunMain(args);
        }
    }
}
