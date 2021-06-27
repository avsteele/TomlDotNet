using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.Collections;

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
            var a = new Tomlet.Models.TomlArray
            {
                5,
                false,
                5.55
            };
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
            var cIn = new Conv(5, 6, 7, 8.8, 9.9F);

            Tomlet.Models.TomlTable tt = new();
            tt.Put("I", cIn.I); // inserted into table as TomlLong -> needs conversion to int
            tt.Put("UI", cIn.UI); // inserted into table as TomlLong -> needs conversion to uint
            tt.Put("UL", cIn.UL); // inserted into table as TomlLong -> needs conversion to ulong
            tt.Put("D", cIn.D); // inserted into table as TomlDouble -> NO CONVERSION
            tt.Put("F", cIn.F); // inserted into table as TomlDouble -> needs convresion to float 

            Toml.Conversions.Add((typeof(long), typeof(int)), (i) => Toml.ToInt((long)i));
            Toml.Conversions.Add((typeof(long), typeof(uint)), (i) => Toml.ToUInt((long)i));
            Toml.Conversions.Add((typeof(long), typeof(ulong)), (i) => Toml.ToULong((long)i));
            Toml.Conversions.Add((typeof(long), typeof(double)), (i) => Convert.ToDouble((long)i));
            Toml.Conversions.Add((typeof(double), typeof(float)), (d) => Convert.ToSingle((double)d));
            var cOut = Toml.Get<Conv>(tt); // attempt extaction of ints with conversions
            Assert.IsTrue(cIn == cOut);
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

    public record Conv(int I, uint UI, ulong UL, double D, float F);
}
