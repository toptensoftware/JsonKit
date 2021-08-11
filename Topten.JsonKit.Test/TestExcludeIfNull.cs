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
    public class TestExcludeIfNull
    {
        class Thing
        {
            [Json("field", ExcludeIfNull = true)]
            public string Field;

            [Json("property", ExcludeIfNull = true)]
            public string Property { get; set; }

            [Json("nfield", ExcludeIfNull = true)]
            public int? NField;

            [Json("nproperty", ExcludeIfNull = true)]
            public int? NProperty { get; set; }

        }

        [Fact]
        public void TestDoesntWriteNull()
        {
            var thing = new Thing();

            // Save it
            var json = Json.Format(thing);

            // Check the object kinds were written out
            Assert.DoesNotContain("\"field\":", json);
            Assert.DoesNotContain("\"property\":", json);
            Assert.DoesNotContain("\"nfield\":", json);
            Assert.DoesNotContain("\"nproperty\":", json);
        }

        [Fact]
        public void TestDoesWriteNonNull()
        {
            var thing = new Thing()
            {
                Field = "blah",
                Property = "deblah",
                NField = 23,
                NProperty = 24,
            };

            // Save it
            var json = Json.Format(thing);

            // Check the object kinds were written out
            Assert.Contains("\"field\":", json);
            Assert.Contains("\"property\":", json);
            Assert.Contains("\"nfield\":", json);
            Assert.Contains("\"nproperty\":", json);
            Assert.Contains("\"blah\"", json);
            Assert.Contains("\"deblah\"", json);
            Assert.Contains("23", json);
            Assert.Contains("24", json);
        }
    }
}
