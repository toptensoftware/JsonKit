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
    public class TestOptions
    {
        [Fact]
        public void TestWhitespace()
        {
            var o = new { x = 10, y = 20 };

            var json = Json.Format(o, JsonOptions.WriteWhitespace);
            Assert.Contains("\n", json);
            Assert.Contains("\t", json);
            Assert.Contains(": ", json);

            json = Json.Format(o, JsonOptions.DontWriteWhitespace);
            Assert.DoesNotContain("\n", json);
            Assert.DoesNotContain("\t", json);
            Assert.DoesNotContain(": ", json);
        }
        
        [Fact]
        public void TestStrictComments()
        {
            var jsonWithCComment = "/* This is a comment*/ 23";
            var jsonWithCppComment = "// This is a comment\n 23";

            // Nonstrict parser allows it
            var val = Json.Parse<int>(jsonWithCComment, JsonOptions.NonStrictParser);
            Assert.Equal(23, val);
            val = Json.Parse<int>(jsonWithCppComment, JsonOptions.NonStrictParser);
            Assert.Equal(23, val);

            // Strict parser
            Assert.Throws<JsonParseException>(() => Json.Parse<int>(jsonWithCComment, JsonOptions.StrictParser));
            Assert.Throws<JsonParseException>(() => Json.Parse<int>(jsonWithCppComment, JsonOptions.StrictParser));
        }

        [Fact]
        public void TestStrictTrailingCommas()
        {
            var arrayWithTrailingComma = "[1,2,]";
            var dictWithTrailingComma = "{\"a\":1,\"b\":2,}";

            // Nonstrict parser allows it
            var array = Json.Parse<int[]>(arrayWithTrailingComma, JsonOptions.NonStrictParser);
            Assert.Equal(2, array.Length);
            var dict = Json.Parse<IDictionary<string, object>>(dictWithTrailingComma, JsonOptions.NonStrictParser);
            Assert.Equal(2, dict.Count);

            // Strict parser
            Assert.Throws<JsonParseException>(() => Json.Parse<int>(arrayWithTrailingComma, JsonOptions.StrictParser));
            Assert.Throws<JsonParseException>(() => Json.Parse<int>(dictWithTrailingComma, JsonOptions.StrictParser));
        }

        [Fact]
        public void TestStrictIdentifierKeys()
        {
            var data = "{a:1,b:2}";

            var dict = Json.Parse<IDictionary<string, object>>(data, JsonOptions.NonStrictParser);
            Assert.Equal(2, dict.Count);
            Assert.Contains("a", dict.Keys);
            Assert.Contains("b", dict.Keys);

            Assert.Throws<JsonParseException>(() => Json.Parse<Dictionary<string, object>>(data, JsonOptions.StrictParser));
        }
    }
}
