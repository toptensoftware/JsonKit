using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;

namespace TestCases
{
    struct MyStruct
    {
        public int x;
        public int y;
    }

    class Program
    {
        static void StructTest()
        {
            /*
            var inst = Activator.CreateInstance(typeof(MyStruct));

            var fi = typeof(MyStruct).GetField("x");
            fi.SetValue(inst, 23);

            var final = (MyStruct)inst;

            int x = 3;
             */

            var method = new DynamicMethod("set_struct_field", null, new Type[] { typeof(object) }, true);
            var il = method.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Unbox, typeof(MyStruct));
            il.Emit(OpCodes.Ldc_I4, 23);
            il.Emit(OpCodes.Stfld, typeof(MyStruct).GetField("x"));

            il.Emit(OpCodes.Ret);

            var fn = (Action<object>)method.CreateDelegate(typeof(Action<object>));

            object inst = new MyStruct();

            fn(inst);


            int x = 3;
        }


        static void Main(string[] args)
        {
            StructTest();
            PetaJson.JsonEmit.Init();
            PetaTest.Runner.RunMain(args);
        }
    }
}
