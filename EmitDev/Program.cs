using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PetaJson;
using System.Globalization;

namespace EmitDev
{
    [Json]
    class Stuff
    {
        public string Name;
        public string Address;
    }

    [Json]
    struct Person : IJsonWriting, IJsonWritten
    {
        public string StringField;
        public int IntField;
        public double DoubleField;
        public bool BoolField;
        public char CharField;
        public DateTime DateTimeField;
        public byte[] BlobField;
        public List<Stuff> StuffsField;

        public string StringProp { get; set; }
        public int IntProp { get; set; }
        public double DoubleProp { get; set; }
        public bool BoolProp { get; set; }
        public char CharProp { get; set; }
        public DateTime DateTimeProp { get; set; }
        public byte[] BlobProp { get; set; }
        public List<Stuff> StuffsProp { get; set; }

        public int? NullableField1;
        public int? NullableField2;
        public int? NullableProp1;
        public int? NullableProp2;

        void IJsonWritten.OnJsonWritten(IJsonWriter w)
        {
            w.WriteRaw("/* OnJsonWritten */ ");
        }

        void IJsonWriting.OnJsonWriting(IJsonWriter w)
        {
            w.WriteRaw("/* OnJsonWriting */");
        }
    }


    class Program
    {

        static void Main(string[] args)
        {
            var p = new Person()
            {
                StringField = "Hello World",
                IntField = 23,
                DoubleField = 99.99,
                BoolField = false,
                CharField = 'X',
                DateTimeField = DateTime.Now,
                BlobField = new byte[] { 1, 2, 3, 4},
                StuffsField = new List<Stuff>() { new Stuff() { Name="Brad", Address="Home" } },

                StringProp = "Hello World",
                IntProp = 23,
                DoubleProp = 99.99,
                BoolProp = false,
                CharProp = 'X',
                DateTimeProp = DateTime.Now,
                BlobProp = new byte[] { 1, 2, 3, 4},
                StuffsProp = new List<Stuff>() { new Stuff() { Name="Brad", Address="Home" } },

                NullableField1 = null,
                NullableField2 = 23,
                NullableProp1 = null,
                NullableProp2 = 23,
            };

            var json = Json.Format(p);
            Console.WriteLine(json);

            var p2 = Json.Parse<Person>(json);

            Console.WriteLine();
            Console.WriteLine();

            var json2 = Json.Format(p2);
            Console.WriteLine(json2);
        }
    }
}
