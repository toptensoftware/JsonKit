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
    class ModelNotDecorated
    {
        public string Field1;
        public string Field2;
        public string Prop1 { get; set; }
        public string Prop2 { get; set; }
    }

    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    class ModelInclude
    {
        [Json] public string Field1;
        public string Field2;
        [Json] public string Prop1 { get; set; }
        public string Prop2 { get; set; }
    }

    [Obfuscation(Exclude = true, ApplyToMembers = true)]
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

    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    class ModelRenamedMembers
    {
        [Json("Field1")] public string Field1;
        public string Field2;
        [Json("Prop1")] public string Prop1 { get; set; }
        public string Prop2 { get; set; }
    }

    [Json]
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    class InstanceObject
    {
        public int IntVal1;

        [JsonExclude] public int IntVal2;

    }

    [Json]
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    class ModelKeepInstance
    {
        [Json(KeepInstance=true)]
        public InstanceObject InstObj;
    }

    [Json]
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    class ModelWithInstance
    {
        [Json]
        public InstanceObject InstObj;
    }

    [Json]
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    struct ModelStruct
    {
        public int IntField;
        public int IntProp { get; set; }
    }

    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public class TestsReflection
    {
        [Fact]
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

            Assert.Contains("field1", json);
            Assert.Contains("field2", json);
            Assert.DoesNotContain("field3", json);
            Assert.Contains("prop1", json);
            Assert.Contains("prop2", json);
            Assert.DoesNotContain("prop3", json);
        }

        [Fact]
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

            Assert.Contains("field1", json);
            Assert.Contains("field2", json);
            Assert.Contains("prop1", json);
            Assert.Contains("prop2", json);
        }

        [Fact]
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

            Assert.Contains("field1", json);
            Assert.DoesNotContain("field2", json);
            Assert.Contains("prop1", json);
            Assert.DoesNotContain("prop2", json);
        }

        [Fact]
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

            Assert.Contains("Field1", json);
            Assert.DoesNotContain("field2", json);
            Assert.Contains("Prop1", json);
            Assert.DoesNotContain("prop2", json);
        }

        [Fact]
        public void KeepInstanceTest1()
        {
            // Create model and save it
            var ki = new ModelKeepInstance();
            ki.InstObj = new InstanceObject();
            ki.InstObj.IntVal1 = 1;
            ki.InstObj.IntVal2 = 2;
            var json = Json.Format(ki);

            // Update the kept instance object
            ki.InstObj.IntVal1 = 11;
            ki.InstObj.IntVal2 = 12;

            // Reload
            var oldInst = ki.InstObj;
            Json.ParseInto(json, ki);

            // Check object instance kept
            Assert.Same(oldInst, ki.InstObj);

            // Check json properties updated, others not
            Assert.Equal(1, ki.InstObj.IntVal1);
            Assert.Equal(12, ki.InstObj.IntVal2);
        }

        [Fact]
        public void KeepInstanceTest2()
        {
            // Create model and save it
            var ki = new ModelKeepInstance();
            ki.InstObj = new InstanceObject();
            ki.InstObj.IntVal1 = 1;
            ki.InstObj.IntVal2 = 2;
            var json = Json.Format(ki);

            // Update the kept instance object
            ki.InstObj = null;

            // Reload
            Json.ParseInto(json, ki);

            // Check object instance kept
            Assert.NotNull(ki.InstObj);

            // Check json properties updated, others not
            Assert.Equal(1, ki.InstObj.IntVal1);
            Assert.Equal(0, ki.InstObj.IntVal2);
        }

        [Fact]
        public void StructTest()
        {
            var o = new ModelStruct();
            o.IntField = 23;
            o.IntProp = 24;

            var json = Json.Format(o);
            Assert.Contains("23", json);
            Assert.Contains("24", json);

            var o2 = Json.Parse<ModelStruct>(json);
            Assert.Equal(23, o2.IntField);
            Assert.Equal(24, o2.IntProp);

            // Test parseInto on a value type not supported
            var o3 = new ModelStruct();
            Assert.Throws<InvalidOperationException>(() => Json.ParseInto(json, o3));
        }

        [Fact]
        public void NullClassMember()
        {
            var m = new ModelWithInstance();
            var json = Json.Format(m);

            Assert.Contains("null", json);

            m.InstObj = new InstanceObject();

            Json.ParseInto(json, m);
            Assert.Null(m.InstObj);
        }

        [Fact]
        public void NullClass()
        {
            // Save null
            var json = Json.Format(null);
            Assert.Equal("null", json);

            // Load null
            var m = Json.Parse<ModelWithInstance>("null");
            Assert.Null(m);

            // Should fail to parse null into an existing instance
            m = new ModelWithInstance();
            Assert.Throws<JsonParseException>(() => Json.ParseInto("null", m));
        }
    }
}
