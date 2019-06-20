using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PetaJson;
using System.Reflection;
using Xunit;

namespace TestCases
{
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public class TestsGeneral
	{
		[Fact]
		public void Format_Null()
		{
			Assert.Equal("null", Json.Format(null));
		}

		[Fact]
		public void Format_Boolean()
		{
			Assert.Equal("true", Json.Format(true));
			Assert.Equal("false", Json.Format(false));
		}

		[Fact]
		public void Format_String()
		{
			Assert.Equal("\"Hello World\"", Json.Format("Hello World"));
            Assert.Equal("\" \\\" \\\\ \\/ \\b \\f \\n \\r \\t \\u0000 \u1234\"", Json.Format(" \" \\ / \b \f \n \r \t \0 \u1234"));
		}

		[Fact]
		public void Format_Numbers()
		{
			Assert.Equal("123", Json.Format(123));
			Assert.Equal("-123", Json.Format(-123));
			Assert.Equal("123", Json.Format(123.0));
			Assert.Equal("123.4", Json.Format(123.4));
			Assert.Equal("-123.4", Json.Format(-123.4));
			Assert.Equal("-1.2345E-65", Json.Format(-123.45E-67));
			Assert.Equal("123", Json.Format(123U));
			Assert.Equal("255", Json.Format(0xFF));
			Assert.Equal("255", Json.Format(0xFFU));
			Assert.Equal("18446744073709551615", Json.Format(0xFFFFFFFFFFFFFFFFL));
		}

		[Fact]
		public void Format_Empty_Array()
		{
			Assert.Equal("[]", Json.Format(new int[] { }));
		}

		[Fact]
		public void Format_Simple_Array()
		{
			Assert.Equal("[\n\t1,\n\t2,\n\t3\n]", Json.Format(new int[] { 1, 2, 3 }));
		}


		[Fact]
		public void Format_Empty_Dictionary()
		{
			Assert.Equal("{}", Json.Format(new Dictionary<int, int>() { }));
		}

		[Fact]
		public void Format_Simple_Dictionary()
		{
			var result = Json.Format(new Dictionary<string, int>() { {"Apples", 1}, {"Pears", 2} , {"Bananas", 3 } });
			Assert.Equal("{\n\t\"Apples\": 1,\n\t\"Pears\": 2,\n\t\"Bananas\": 3\n}", result);
		}

		[Fact]
		public void Format_Date()
		{
			Assert.Equal("1293840000000", Json.Format(new DateTime(2011, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
		}

		[Fact]
		public void Format_Poco()
		{
			var result = Json.Format(new { Apples=1, Pears=2, Bananas=3});
			Assert.Equal("{\n\t\"apples\": 1,\n\t\"pears\": 2,\n\t\"bananas\": 3\n}", result);
		}

		[Fact]
		public void Parse_Null()
		{
			Assert.Null(Json.Parse<object>("null"));
		}

		[Fact]
		public void Parse_Boolean()
		{
			Assert.True(Json.Parse<bool>("true"));
			Assert.False(Json.Parse<bool>("false"));
		}

		[Fact]
		public void Parse_String()
		{
			var s = Json.Parse<string>("\"Hello\\r\\n\\t\\u0000 World\"");
			Assert.Equal("Hello\r\n\t\0 World", (string)s);
		}

		[Fact]
		public void Parse_Numbers()
		{
			Assert.Equal(0, Json.Parse<int>("0"));
			Assert.Equal(123, Json.Parse<int>("123"));
			Assert.Equal(123.45, Json.Parse<double>("123.45"));

			Assert.Equal(123e45, Json.Parse<double>("123e45"));
			Assert.Equal(123.0e45, Json.Parse<double>("123.0e45"));
			Assert.Equal(123e45, Json.Parse<double>("123e+45"));
			Assert.Equal(123.0e45, Json.Parse<double>("123.0e+45"));
			Assert.Equal(123e-45, Json.Parse<double>("123e-45"));
			Assert.Equal(123.0e-45, Json.Parse<double>("123.0e-45"));

			Assert.Equal(123E45, Json.Parse<double>("123E45"));
			Assert.Equal(-123e45, Json.Parse<double>("-123e45"));
		}

		[Fact]
		public void Parse_Empty_Array()
		{
			var d = Json.Parse<object[]>("[]");
			Assert.Equal(new object[] { }, d as object[]);
       }

		[Fact]
		public void Parse_simple_Array()
		{
			var d = Json.Parse<int[]>("[1,2,3]");
			Assert.Equal(new int[] { 1, 2, 3} , d);
        }

		[Fact]
		public void Parse_Date()
		{
			var d1 = new DateTime(2011, 1, 1, 10, 10, 10, DateTimeKind.Utc);
			var d2 = Json.Parse<DateTime>(Json.Format(d1));
			Assert.Equal(d2, d1);
		}

		[Fact]
		public void DynamicTest()
		{
			var d = Json.Parse<IDictionary<string, object>>("{\"apples\":1, \"pears\":2, \"bananas\":3}") ;

			Assert.Equal(new string[] { "apples", "pears", "bananas" }, d.Keys);
			Assert.Equal(new object[] { 1UL, 2UL, 3UL }, d.Values);
		}

		[Fact]
		public void Invalid_Numbers()
		{
			Assert.Throws<JsonParseException>(() => Json.Parse<object>("123ee0"));
            Assert.Throws<JsonParseException>(() => Json.Parse<object>("+123"));
            Assert.Throws<JsonParseException>(() => Json.Parse<object>("--123"));
            Assert.Throws<JsonParseException>(() => Json.Parse<object>("--123..0"));
            Assert.Throws<JsonParseException>(() => Json.Parse<object>("--123ex"));
            Assert.Throws<JsonParseException>(() => Json.Parse<object>("123x"));
            Assert.Throws<JsonParseException>(() => Json.Parse<object>("0x123", JsonOptions.StrictParser));
        }
		[Fact]
		public void Invalid_Trailing_Characters()
		{
			Assert.Throws<JsonParseException>(()=> Json.Parse<object>("\"Hello\" , 123"));
		}

		[Fact]
		public void Invalid_Identifier()
		{
			Assert.Throws<JsonParseException>(() => Json.Parse<object>("identifier"));
        }

		[Fact]
		public void Invalid_Character()
		{
			Assert.Throws<JsonParseException>(() => Json.Parse<object>("~"));
        }

		[Fact]
		public void Invalid_StringEscape()
		{
			Assert.Throws<JsonParseException>(() => Json.Parse<object>("\"\\q\""));
        }

        [Fact]
        public void ErrorLocation()
        {
            var strJson="{\r\n \r\n \n\r \r \n \t   \"key:\": zzz";
            try
            {
                Json.Parse<object>(strJson);
            }
            catch (JsonParseException x)
            {
                Assert.Equal(5, x.Position.Line);
                Assert.Equal(13, x.Position.Offset);
            }
        }
    }
}
