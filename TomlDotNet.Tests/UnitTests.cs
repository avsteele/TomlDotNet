using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;
using System;
using System.Collections.Generic;

using Tomlet;
using TomlDotNet;

namespace TomlDotNet.Tests
{
    [TestClass]
    public class TestToml
    {
        [TestMethod]
        public void BasicTypes()
        {
            var data = new Data(5, 0.123, "hello", true);
            var tt = new Tomlet.Models.TomlTable();
            tt.Put("L", data.L);
            tt.Put("D", data.D);
            tt.Put("S", data.S);
            tt.Put("B", data.B);
            var dout = TomlDotNet.Toml.Get<Data>(tt);
            Assert.IsTrue(data == dout);
        }

        [TestMethod]
        public void Array()
        {
            var a = new Tomlet.Models.TomlArray();
            a.Add(5);
            a.Add(false);
            a.Add(5.55);
            // TODO: can't do a string because .Add doesn't allow it??! pretty sure TOML allows arrays of string s though...
            var tt = new Tomlet.Models.TomlTable();
            tt.PutValue("A", a);
            var r = new ArrayHolder(new() { 5L, false, 5.55 });
            //var arr = TomlDotNet.Toml.ConvertArray(a);
            Assert.IsTrue(((Tomlet.Models.TomlLong)a[0]).Value == (long)r.A[0]);
            Assert.IsTrue(((Tomlet.Models.TomlBoolean)a[1]).Value == (bool)r.A[1]);
            Assert.IsTrue(((Tomlet.Models.TomlDouble)a[2]).Value == (double)r.A[2]);
        }

        [TestMethod]
        public void DateTimes()
        {
            throw new NotImplementedException();
        }

        [TestMethod]
        public void Converters()
        {
            int i = 5;
            long l = int.MaxValue + 1L;

            //var i2 = Convert.ToInt32(l);
            var d = Convert.ToDouble(long.MaxValue);
            Console.WriteLine(d);
        }

        [TestMethod]
        public void NestedTypes()
        {
            var data = new Nested(new Inner(5));
            var I = new Tomlet.Models.TomlTable();
            I.Put("L", 5);
            var tt = new Tomlet.Models.TomlTable();
            // using 'Put' instead of putvalue here does something odd
            tt.PutValue("I", I);
            var dOut = Toml.Get<Nested>(tt);
            Assert.IsTrue(dOut.I.L == data.I.L);
        }
    }

    public record Data(long L, double D, string S, bool B);
    public record Nested(Inner I);
    public record Inner(long L);

    public record ArrayHolder(List<object> A);
}
