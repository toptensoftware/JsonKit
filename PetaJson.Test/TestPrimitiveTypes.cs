using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PetaJson;
using System.Reflection;
using Xunit;

namespace TestCases
{
    [Json]
    class AllTypes
    {
        public string String;
        public char Char;
        public bool Bool;
        public byte Byte;
        public sbyte SByte;
        public short Short;
        public ushort UShort;
        public int Int;
        public uint UInt;
        public long Long;
        public ulong ULong;
        public decimal Decimal;
        public float Float;
        public double Double;
        public DateTime DateTime;
        public byte[] Blob;

        public void Init()
        {
            String = "PetaJson!";
            Char = 'J';
            Bool = false;
            Byte = 1;
            SByte = 2;
            Short = 3;
            UShort = 4;
            Int = 5;
            UInt = 6;
            Long = 7;
            ULong = 8;
            Decimal = 9.1M;
            Float = 10.2f;
            Double = 11.3;
            DateTime = new DateTime(2014, 1, 1, 13, 23, 24);
            Blob = new byte[] { 12, 13, 14, 15 };
        }
    }

    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public class TestPrimitiveTypes
    {
        void Compare(AllTypes all, AllTypes all2)
        {
            Assert.Equal(all.String, all2.String);
            Assert.Equal(all.Char, all2.Char);
            Assert.Equal(all.Bool, all2.Bool);
            Assert.Equal(all.Byte, all2.Byte);
            Assert.Equal(all.SByte, all2.SByte);
            Assert.Equal(all.Short, all2.Short);
            Assert.Equal(all.UShort, all2.UShort);
            Assert.Equal(all.Int, all2.Int);
            Assert.Equal(all.UInt, all2.UInt);
            Assert.Equal(all.Long, all2.Long);
            Assert.Equal(all.ULong, all2.ULong);
            Assert.Equal(all.Decimal, all2.Decimal);
            Assert.Equal(all.Float, all2.Float);
            Assert.Equal(all.Double, all2.Double);
            Assert.Equal(all.DateTime, all2.DateTime);
            Assert.True((all.Blob==null && all2.Blob==null) || Convert.ToBase64String(all.Blob)==Convert.ToBase64String(all2.Blob));
        }

        [Fact]
        public void TestBasics()
        {
            var all = new AllTypes();
            all.Init();
            var json = Json.Format(all);
            var all2 = Json.Parse<AllTypes>(json);

            Compare(all, all2);
        }

        [Fact]
        public void TestNegatives()
        {
            var all = new AllTypes();
            all.Init();
            all.SByte = -1;
            all.Short = -2;
            all.Int = -3;
            all.Long = -4;
            all.Decimal = -5.1M;
            all.Float = -6.2f;
            all.Double = -7.3;

            var json = Json.Format(all);
            var all2 = Json.Parse<AllTypes>(json);
            Compare(all, all2);
        }

        [Fact]
        public void TestMaxValue()
        {
            var all = new AllTypes();
            all.Init();
            all.Bool = true;
            all.Byte = Byte.MaxValue;
            all.SByte = SByte.MaxValue;
            all.Short = short.MaxValue;
            all.UShort = ushort.MaxValue;
            all.Int = int.MaxValue;
            all.UInt = uint.MaxValue;
            all.Long = long.MaxValue;
            all.ULong = ulong.MaxValue;
            all.Decimal = decimal.MaxValue;
            all.Float = float.MaxValue;
            all.Double = double.MaxValue;

            var json = Json.Format(all);
            Console.WriteLine(json);
            var all2 = Json.Parse<AllTypes>(json);
            Compare(all, all2);
        }

        [Fact]
        public void TestMinValue()
        {
            var all = new AllTypes();
            all.String = null;
            all.Bool = false;
            all.Byte = Byte.MinValue;
            all.SByte = SByte.MinValue;
            all.Short = short.MinValue;
            all.UShort = ushort.MinValue;
            all.Int = int.MinValue;
            all.UInt = uint.MinValue;
            all.Long = long.MinValue;
            all.ULong = ulong.MinValue;
            all.Decimal = decimal.MinValue;
            all.Float = float.MinValue;
            all.Double = double.MinValue;
            all.Blob = null;

            var json = Json.Format(all);
            Console.WriteLine(json);
            var all2 = Json.Parse<AllTypes>(json);
            Compare(all, all2);
        }
    }
}
