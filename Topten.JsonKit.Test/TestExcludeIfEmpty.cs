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
    public class TestExcludeIfEmpty
    {
        class Thing
        {
            [Json("array", ExcludeIfEmpty = true)]
            public string[] Array;

            [Json("dictionary", ExcludeIfEmpty = true)]
            public Dictionary<string, object> Dictionary;

            [Json("list", ExcludeIfEmpty = true)]
            public List<string> List;
        }

        [Fact]
        public void TestDoesntWriteNull()
        {
            var thing = new Thing();

            // Save it
            var json = Json.Format(thing);

            // Check the object kinds were written out
            Assert.DoesNotContain("\"array\":", json);
            Assert.DoesNotContain("\"dictionary\":", json);
            Assert.DoesNotContain("\"list\":", json);
        }

        [Fact]
        public void TestDoesntWriteEmpty()
        {
            var thing = new Thing()
            {
                Array = new string[0],
                Dictionary = new Dictionary<string, object>(),
                List = new List<string>(),
            };

            // Save it
            var json = Json.Format(thing);

            // Check the object kinds were written out
            Assert.DoesNotContain("\"array\":", json);
            Assert.DoesNotContain("\"dictionary\":", json);
            Assert.DoesNotContain("\"list\":", json);
        }

        [Fact]
        public void TestDoesWriteNonEmpty()
        {
            var thing = new Thing()
            {
                Array = new string[] { "apples" },
                Dictionary = new Dictionary<string, object>() { { "pears", true } },
                List = new List<string> { "bananas" },
            };

            // Save it
            var json = Json.Format(thing);

            // Check the object kinds were written out
            Assert.Contains("\"array\":", json);
            Assert.Contains("\"dictionary\":", json);
            Assert.Contains("\"list\":", json);
            Assert.Contains("\"apples\"", json);
            Assert.Contains("\"pears\"", json);
            Assert.Contains("\"bananas\"", json);
        }
    }
}
