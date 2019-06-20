using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PetaJson;
using Xunit;

namespace TestCases
{
    public class TestDictionaryUtils
    {
        [Fact]
        public void DictionaryPaths()
        {
            var dict = new Dictionary<string, object>();
            dict.SetPath("settings.subSettings.settingA", 23);
            dict.SetPath("settings.subSettings.settingB", 24);

            Assert.True(dict.ContainsKey("settings"));
            Assert.True(((IDictionary<string, object>)dict["settings"]).ContainsKey("subSettings"));
            Assert.Equal(23, dict.GetPath<int>("settings.subSettings.settingA"));
            Assert.Equal(24, dict.GetPath<int>("settings.subSettings.settingB"));
            Assert.True(dict.PathExists("settings.subSettings"));
            Assert.True(dict.PathExists("settings.subSettings.settingA"));
            Assert.False(dict.PathExists("missing_in_action"));
        }

        [Fact]
        public void DictionaryReparseType()
        {
            // Create and initialize and object then convert it to a dictionary
            var o = new DaObject() { id = 101, Name = "#101" };
            var oDict = Json.Reparse<IDictionary<string, object>>(o);

            // Store that dictionary at a path inside another dictionary
            var dict = new Dictionary<string, object>();
            dict.SetPath("settings.daObject", oDict);

            // Get it back out, but reparse it back into a strongly typed object
            var o2 = dict.GetPath<DaObject>("settings.daObject");
            Assert.Equal(o2.id, o.id);
            Assert.Equal(o2.Name, o.Name);
        }

        [Fact]
        public void ObjectAtPath()
        {
            // Create and initialize and object then convert it to a dictionary
            var o = new DaObject() { id = 101, Name = "#101" };
            var oDict = Json.Reparse<IDictionary<string, object>>(o);

            // Store that dictionary at a path inside another dictionary
            var dict = new Dictionary<string, object>();
            dict.SetPath("settings.daObject", oDict);

            // Get it back as an object (and update dict to hold an actual DaObject
            var o2 = dict.GetObjectAtPath<DaObject>("settings.daObject");

            // Modify it
            o2.id = 102;
            o2.Name = "modified";

            // Save the dictionary and make sure we got the change
            var json = Json.Format(dict);
            Assert.Contains("102", json);
            Assert.Contains("modified", json);
        }

        [Fact]
        public void NewObjectAtPath()
        {
            // Create a new object at a path
            var dict = new Dictionary<string, object>();
            var o2 = dict.GetObjectAtPath<DaObject>("settings.daObject");

            // Modify it
            o2.id = 103;
            o2.Name = "new guy";

            // Save the dictionary and make sure we got the change
            var json = Json.Format(dict);
            Assert.Contains("103", json);
            Assert.Contains("new guy", json);
        }
    }
}
