using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PetaTest;
using PetaJson;

namespace TestCases
{
    class ModelNotDecorated
    {
        public string Field1;
        public string Field2;
        public string Prop1 { get; set; }
        public string Prop2 { get; set; }
    }

    class ModelInclude
    {
        [Json] public string Field1;
        public string Field2;
        [Json] public string Prop1 { get; set; }
        public string Prop2 { get; set; }
    }

    class ModelExclude
    {
        public string Field1;
        public string Field2;
        public string Prop1 { get; set; }
        public string Prop2 { get; set; }

        [JsonExclude]
        public string Field3;

        [JsonExclude]
        public string Prop3 { get; set; }
    }

    class ModelRenamedMembers
    {
        [Json("Field1")] public string Field1;
        public string Field2;
        [Json("Prop1")] public string Prop1 { get; set; }
        public string Prop2 { get; set; }
    }

    [TestFixture]
    public class TestsReflection
    {
        [Test]
        public void ExcludeAttribute()
        {
            var m = new ModelExclude()
            {
                Field1 = "f1",
                Field2 = "f2",
                Field3 = "f3", 
                Prop1 = "p1",
                Prop2 = "p2",
                Prop3 = "p3",
            };

            var json = Json.Format(m);

            Assert.Contains(json, "field1");
            Assert.Contains(json, "field2");
            Assert.DoesNotContain(json, "field3");
            Assert.Contains(json, "prop1");
            Assert.Contains(json, "prop2");
            Assert.DoesNotContain(json, "prop3");
        }

        [Test]
        public void NonDecorated()
        {
            var m = new ModelNotDecorated()
            {
                Field1 = "f1",
                Field2 = "f2",
                Prop1 = "p1",
                Prop2 = "p2",
            };

            var json = Json.Format(m);

            Assert.Contains(json, "field1");
            Assert.Contains(json, "field2");
            Assert.Contains(json, "prop1");
            Assert.Contains(json, "prop2");
        }

        [Test]
        public void Include()
        {
            var m = new ModelInclude()
            {
                Field1 = "f1",
                Field2 = "f2",
                Prop1 = "p1",
                Prop2 = "p2",
            };

            var json = Json.Format(m);

            Assert.Contains(json, "field1");
            Assert.DoesNotContain(json, "field2");
            Assert.Contains(json, "prop1");
            Assert.DoesNotContain(json, "prop2");
        }

        [Test]
        public void RenamedMembers()
        {
            var m = new ModelRenamedMembers()
            {
                Field1 = "f1",
                Field2 = "f2",
                Prop1 = "p1",
                Prop2 = "p2",
            };

            var json = Json.Format(m);

            Assert.Contains(json, "Field1");
            Assert.DoesNotContain(json, "field2");
            Assert.Contains(json, "Prop1");
            Assert.DoesNotContain(json, "prop2");
        }
    }
}
