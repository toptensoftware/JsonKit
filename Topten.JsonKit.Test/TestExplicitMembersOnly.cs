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
    public class TestExplicitMembersOnly
    {
        class Thing
        {
            public string Apples = "apples";
        }

        [Json(ExplicitMembersOnly = true)]
        class Thing2
        {
            public string Apples = "apples";
        }

        [Json(ExplicitMembersOnly = true)]
        class Thing3
        {
            [Json("apples")]
            public string Apples = "apples";
        }

        [Fact]
        public void TestNonDecoratedClass()
        {
            var thing = new Thing();

            // Save it
            var json = Json.Format(thing);

            // Check the object kinds were written out
            Assert.Contains("\"apples\":", json);
        }

        [Fact]
        public void TestDecoratedEmptyClass()
        {
            var thing = new Thing2();

            // Save it
            var json = Json.Format(thing);

            // Check the object kinds were written out
            Assert.DoesNotContain("\"apples\":", json);
        }

        [Fact]
        public void TestDecoratedNonEmptyClass()
        {
            var thing = new Thing3();

            // Save it
            var json = Json.Format(thing);

            // Check the object kinds were written out
            Assert.Contains("\"apples\":", json);
        }

    }
}
