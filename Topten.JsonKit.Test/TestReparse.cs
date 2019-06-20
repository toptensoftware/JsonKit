using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Topten.JsonKit;
using Xunit;

namespace TestCases
{
    class DaObject
    {
        [Json] public long id;
        [Json] public string Name;
    }

    public class TestReparse
    {
        void Compare(DaObject a, DaObject b)
        {
            Assert.Equal(a.id, b.id);
            Assert.Equal(a.Name, b.Name);
        }

        [Fact]
        public void Clone()
        {
            var a = new DaObject() { id = 101, Name = "#101" };
            var b = Json.Clone(a);
            Compare(a, b);
        }

        [Fact]
        public void Reparse()
        {
            var a = new DaObject() { id = 101, Name = "#101" };
            var dict = Json.Reparse<IDictionary<string, object>>(a);

            Assert.Equal(101UL, dict["id"]);
            Assert.Equal("#101", dict["name"]);

            var b = Json.Reparse<DaObject>(dict);

            Compare(a, b);
        }
    }
}
