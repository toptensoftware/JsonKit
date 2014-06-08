using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PetaTest;
using PetaJson;
using System.IO;
using System.Globalization;

namespace TestCases
{
    struct Point
    {
        public int X;
        public int Y;
    }

    [TestFixture]
    public class TestCustomFormat
    {
        static TestCustomFormat()
        {
            // Custom formatter
            Json.RegisterFormatter<Point>( (writer,point) => 
            {
                writer.WriteStringLiteral(string.Format("{0},{1}", point.X, point.Y));
            });

            // Custom parser
            Json.RegisterParser<Point>( literal => {

                var parts = ((string)literal).Split(',');
                if (parts.Length!=2)
                    throw new InvalidDataException("Badly formatted point");

                return new Point()
                {
                    X = int.Parse(parts[0], CultureInfo.InvariantCulture),
                    Y = int.Parse(parts[0], CultureInfo.InvariantCulture),
                };

            });
        }

        [Test]
        public void Test()
        {
            var p = new Point() { X = 10, Y = 20 };

            var json = Json.Format(p);

            Assert.AreEqual(json, "\"10,20\"");

            var p2 = Json.Parse<Point>(json);

            Assert.Equals(p.X, p2.X);
            Assert.Equals(p.Y, p2.Y);
        }

        [Test]
        public void TestExceptionPassed()
        {
            Assert.Throws<JsonParseException>(() => Json.Parse<Point>("\"10,20,30\""));
        }
    }
}
