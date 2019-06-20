using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Topten.JsonKit;
using System.Reflection;
using Xunit;

namespace TestCases
{
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    class NullableContainer
    {
        public int? Field;
        public int? Prop { get; set; }
    }

    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public class TestNullableTypes
    {
        [Fact]
        public void TestNull()
        {
            var nc = new NullableContainer();

            var json = Json.Format(nc);
            Console.WriteLine(json);
            Assert.Contains("null", json);

            var nc2 = Json.Parse<NullableContainer>(json);
            Assert.Null(nc2.Field);
            Assert.Null(nc2.Prop);
        }

        [Fact]
        public void TestNotNull()
        {
            var nc = new NullableContainer()
            {
                Field = 23,
                Prop = 24,
            };

            var json = Json.Format(nc);
            Console.WriteLine(json);
            Assert.DoesNotContain(json, "null");

            var nc2 = Json.Parse<NullableContainer>(json);
            Assert.Equal(23, nc2.Field.Value);
            Assert.Equal(24, nc2.Prop.Value);
        }
    }
}
