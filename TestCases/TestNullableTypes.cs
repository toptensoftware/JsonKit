using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PetaTest;
using PetaJson;

namespace TestCases
{
    class NullableContainer
    {
        public int? Field;
        public int? Prop { get; set; }
    }

    [TestFixture]
    public class TestNullableTypes
    {
        [Test]
        public void TestNull()
        {
            var nc = new NullableContainer();

            var json = Json.Format(nc);
            Console.WriteLine(json);
            Assert.Contains(json, "null");

            var nc2 = Json.Parse<NullableContainer>(json);
            Assert.IsNull(nc2.Field);
            Assert.IsNull(nc2.Prop);
        }

        [Test]
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
            Assert.AreEqual(nc2.Field.Value, 23);
            Assert.AreEqual(nc2.Prop.Value, 24);
        }
    }
}
