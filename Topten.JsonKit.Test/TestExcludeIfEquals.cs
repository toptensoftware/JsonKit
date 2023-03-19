using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Topten.JsonKit;
using System.IO;
using System.Reflection;
using Xunit;

namespace TestCases
{
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public class TestExcludeIfEquals
    {
        enum Fruit
        {
            Apples,
            Pears,
            Bananas,
        }
        class Thing
        {
            [Json("boolField", ExcludeIfEquals = false)]
            public bool boolField;

            [Json("intField", ExcludeIfEquals = 0)]
            public int intField;

            [Json("boolProperty", ExcludeIfEquals = false)]
            public bool boolProperty { get; set; }

            [Json("intProperty", ExcludeIfEquals = 0)]
            public int intProperty { get; set; }

            [Json("enumField", ExcludeIfEquals = Fruit.Apples)]
            public Fruit enumField;

            [Json("enumProperty", ExcludeIfEquals = Fruit.Apples)]
            public Fruit enumProperty { get; set; }

            [Json("ulongProperty", ExcludeIfEquals = 0UL)]
            public ulong ulongProperty { get; set; }

        }

        public static object GetDefault(Type type)
        {
            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }
            return null;
        }

        [Fact]
        public void TestDoesntWrite()
        {
            var thing = new Thing()
            {
                boolField = false,
                intField = 0,
                boolProperty = false,
                intProperty = 0,
                ulongProperty = 0,
                enumField = Fruit.Apples,
                enumProperty = Fruit.Apples,
            };

            // Save it
            var json = Json.Format(thing);

            // Check the object kinds were written out
            Assert.DoesNotContain("\"boolField\":", json);
            Assert.DoesNotContain("\"intField\":", json);
            Assert.DoesNotContain("\"boolProperty\":", json);
            Assert.DoesNotContain("\"intProperty\":", json);
            Assert.DoesNotContain("\"enumField\":", json);
            Assert.DoesNotContain("\"ulongProperty\":", json);
            Assert.DoesNotContain("\"enumProperty\":", json);
        }

        [Fact]
        public void TestDoesWriteNonNull()
        {
            var thing = new Thing()
            {
                boolField = true,
                intField = 23,
                boolProperty = true,
                intProperty = 24,
                ulongProperty = 25,
                enumField = Fruit.Pears,
                enumProperty = Fruit.Bananas,
            };

            // Save it
            var json = Json.Format(thing);

            // Check the object kinds were written out
            Assert.Contains("\"boolField\":", json);
            Assert.Contains("\"intField\":", json);
            Assert.Contains("\"boolProperty\":", json);
            Assert.Contains("\"intProperty\":", json);
            Assert.Contains("\"enumField\":", json);
            Assert.Contains("\"enumProperty\":", json);
            Assert.Contains("\"ulongProperty\":", json);
            Assert.Contains("true", json);
            Assert.Contains("23", json);
            Assert.Contains("24", json);
            Assert.Contains("Pears", json);
            Assert.Contains("Bananas", json);
        }
    }
}
