using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PetaJson;
using System.IO;
using System.Reflection;
using Xunit;

namespace TestCases
{
    abstract class Shape : IJsonWriting
    {
        [Json("color")] public string Color;

        // Override OnJsonWriting to write out the derived class type
        void IJsonWriting.OnJsonWriting(IJsonWriter w)
        {
            w.WriteKey("kind");
            w.WriteStringLiteral(GetType().Name);
        }
    }

    class Rectangle : Shape
    {
        [Json("cornerRadius")]
        public float CornerRadius;
    }

    class Ellipse : Shape
    {
        [Json("filled")]
        public bool Filled;
    }

    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public class TestAbstractTypes
    {
        static TestAbstractTypes()
        {
            // Register a type factory that can instantiate Shape objects
            Json.RegisterTypeFactory(typeof(Shape), (reader, key) =>
            {
                // This method will be called back for each key in the json dictionary
                // until an object instance is returned

                // We saved the object type in a key called "kind", look for it
                if (key != "kind")
                    return null;

                // Read the next literal (which better be a string) and instantiate the object
                return reader.ReadLiteral(literal =>
                {
                    var className = (string)literal;
                    if (className == typeof(Rectangle).Name)
                        return new Rectangle();

                    if (className == typeof(Ellipse).Name)
                        return new Ellipse();

                    throw new InvalidDataException(string.Format("Unknown shape kind: '{0}'", literal));
                });
            });
        }

        [Fact]
        public void Test()
        {
            // Create a list of shapes
            var shapes = new List<Shape>();
            shapes.Add(new Rectangle() { Color = "Red", CornerRadius = 10 });
            shapes.Add(new Ellipse() { Color="Blue", Filled = true });

            // Save it
            var json = Json.Format(shapes);

            Console.WriteLine(json);

            // Check the object kinds were written out
            Assert.Contains("\"kind\":", json);

            // Reload the list
            var shapes2 = Json.Parse<List<Shape>>(json);

            // Check stuff...
            Assert.Equal(2, shapes2.Count);
            Assert.IsType<Rectangle>(shapes2[0]);
            Assert.IsType<Ellipse>(shapes2[1]);

            Assert.Equal("Red", ((Rectangle)shapes2[0]).Color);
            Assert.Equal(10, ((Rectangle)shapes2[0]).CornerRadius);
            Assert.Equal("Blue", ((Ellipse)shapes2[1]).Color);
            Assert.True(((Ellipse)shapes2[1]).Filled);

        }
    }
}
