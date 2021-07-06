using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

using Tomlet.Models;

namespace TomlDotNet.Tests
{
    /// <summary>
    /// Tests of TomlDotNet's deserialization
    /// </summary>
    [TestClass]
    public class Deserialization
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
            var dout = Deserialize.FromToml<Data>(tt);
            Assert.IsTrue(data == dout);
        }

        [TestMethod]
        public void BasicDeserialization()
        {
            var dIn = TomlDotNet.Deserialize.FromFile<Data>("Example.toml");

            Assert.IsTrue((dIn.L == 5) && (dIn.D == 0.123) && (dIn.S=="hello") && (dIn.B == true));
        }




        public bool SameList<T>(List<T> l1, List<T> l2) where T : IComparable
            => (from el in l1.Zip(l2) where el.First.CompareTo(el.Second) == 0 select 0).Any();

        /// <summary>
        /// Test whether Datetime types can be put into toml form and then rad back into a CLR record
        /// Datetimes are serialized without an offset at the end, and without respect to the 'Kind' field. therefore
        /// ther is an ambigutuity.
        /// The optionare to always use the same 'kind' of DateTime, or alwasy use DateTimeOffset
        /// See https://github.com/paiden/Nett/blob/master/dfx/articles/pitfalls.md#datetime-vs-datetimeoffset
        /// </summary>
        [TestMethod]
        public void DateTimes()
        {
            var dtLocal = DateTime.Now;
            var dtUtc = dtLocal.ToUniversalTime();
            var dto = new DateTimeOffset(dtLocal);
            var dtIn = new DatesTimes(dtLocal, dtUtc, dto);

            Tomlet.Models.TomlDocument tt = Tomlet.Models.TomlDocument.CreateEmpty();
            tt.Put("DT", dtIn.DT);
            tt.Put("DTUtc", dtIn.DTUtc);
            tt.Put("Dto", dtIn.Dto);

            var dtOut = Deserialize.FromToml<DatesTimes>(tt);

            Assert.IsTrue(dtIn == dtOut);
        }

        [TestMethod]
        public void DateTimeFile()
        {
            var dtLocal = DateTime.Now;
            var dtUtc = dtLocal.ToUniversalTime();
            var dto = new DateTimeOffset(dtLocal);

            var dtIn = new DatesTimes(dtLocal, dtUtc, dto);

            Tomlet.Models.TomlDocument tt = Tomlet.Models.TomlDocument.CreateEmpty();
            tt.Put("DT", dtIn.DT); // serialized without offset time
            tt.Put("DTUtc", dtIn.DTUtc); //serializded withut offset time
            tt.Put("Dto", dtIn.Dto); // serialized with offsettime


            // now write t file and back to see if same
            var filename = @"dateTimeFile.toml";
            System.IO.File.WriteAllText(filename, tt.SerializedValue);
            
            var dtIn2 = TomlDotNet.Deserialize.FromFile<DatesTimes>(filename);

            Assert.IsTrue(dtIn == dtIn2);
        }

        [TestMethod]
        public void Converters()
        {
            var cIn = new Conv(4L, 5, 6, 7, 100L, 1001L, 8.8, 9.9F);

            Tomlet.Models.TomlTable tt = new();
            tt.Put("L", cIn.L); // inserted as TomlLong : NO CONVERSION
            tt.Put("I", cIn.I); // inserted as TomlLong : needs long=>int
            tt.Put("UI", cIn.UI); // inserted as TomlLong : needs long=> uint
            tt.Put("UL", cIn.UL); // inserted as TomlLong : needs long=> ulong
            tt.Put("LtoF", (long)cIn.LtoF); // inserted as TomlLong : long=>float
            tt.Put("LtoD", (long)cIn.LtoD); // inserted as TomlLong : long=>double
            tt.Put("D", cIn.D); // inserted as TomlDouble : NO CONVERSION
            tt.Put("F", cIn.F); // inserted as TomlDouble : needs double=>float

            Deserialize.Conversions.Add((typeof(long), typeof(int)), (i) => Convert.ToInt32((long)i));
            Deserialize.Conversions.Add((typeof(long), typeof(uint)), (i) => Convert.ToUInt32((long)i));
            Deserialize.Conversions.Add((typeof(long), typeof(ulong)), (i) => Convert.ToUInt64((long)i));
            Deserialize.Conversions.Add((typeof(long), typeof(float)), (i) => Convert.ToSingle((long)i));
            Deserialize.Conversions.Add((typeof(long), typeof(double)), (i) => Convert.ToDouble((long)i));
            Deserialize.Conversions.Add((typeof(double), typeof(float)), (d) => Convert.ToSingle((double)d));

            var cOut = Deserialize.FromToml<Conv>(tt); // attempt extaction full clas data, requires conversions
            Assert.IsTrue(cIn == cOut);
        }

        [TestMethod]
        public void ConvertersFromFile()
        {
            //     public record Conv(long L, int I, uint UI, ulong UL, double LtoD, float LtoF, double D, float F);

            Deserialize.Conversions.Clear();
            Deserialize.Conversions.Add((typeof(long), typeof(int)), (i) => Convert.ToInt32((long)i));
            Deserialize.Conversions.Add((typeof(long), typeof(uint)), (i) => Convert.ToUInt32((long)i));
            Deserialize.Conversions.Add((typeof(long), typeof(ulong)), (i) => Convert.ToUInt64((long)i));
            Deserialize.Conversions.Add((typeof(long), typeof(float)), (i) => Convert.ToSingle((long)i));
            Deserialize.Conversions.Add((typeof(long), typeof(double)), (i) => Convert.ToDouble((long)i));
            Deserialize.Conversions.Add((typeof(double), typeof(float)), (d) => Convert.ToSingle((double)d));

            var cOut = TomlDotNet.Deserialize.FromFile<Conv>("Conversions.toml"); // attempt extaction full clas data, requires conversions
            Assert.IsTrue(cOut.UL == 8);
            ;
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
            var dOut = Deserialize.FromToml<Nested>(tt);
            Assert.IsTrue(dOut.I.L == data.I.L);
        }

        [TestMethod]
        public void TestConstructorFilter()
        {
            var cIn = new Conv(4L, 5, 6, 7, 100L, 1001L, 8.8, 9.9F);

            Tomlet.Models.TomlTable tt = new();
            tt.Put("L", cIn.L); 
            tt.Put("I", cIn.I); 
            tt.Put("UI", cIn.UI); 
            tt.Put("UL", cIn.UL); 
            tt.Put("LtoF", (long)cIn.LtoF);
            tt.Put("LtoD", (long)cIn.LtoD); 
            tt.Put("D", cIn.D); 
            tt.Put("F", cIn.F); 

            var cs = Deserialize.ConstructorTryOrder(typeof(ManyConstructors), tt);
            Assert.IsTrue(cs.Count == 3);
        }

        [TestMethod]
        public void NoValidConstructor()
        {
            var cIn = new Data(1L, 2.0, "hi", true);

            Tomlet.Models.TomlTable tt = new();
            tt.Put("L", cIn.L);
            tt.Put("D", cIn.D);
            tt.Put("S", cIn.S);
            //tt.Put("B", cIn.B);
            try
            {
                var cOut = TomlDotNet.Deserialize.FromToml<Data>(tt);
            }
            catch { return; }
            throw new Exception("Should have thrown because missing required elements");
        }

        [TestMethod]
        public void Optional()
        {
            Tomlet.Models.TomlTable tt = new();
            tt.Put("L", 1L);
            tt.Put("D", 2.0);
            // tt missing two fields needed to fill Optional constructor, but they are optional so this should be OK
            var cOut = TomlDotNet.Deserialize.FromToml<Optional>(tt);
        }

        [TestMethod]
        public void ArrayOfTables()
        {
            throw new NotImplementedException();
            var fname = "ArrayOfTables.toml";
            var tt = Tomlet.TomlParser.ParseFile(fname);
            ;
        }

        [TestMethod]
        public void HomoArrayTest()
        {
            var r = new HomoArray(new() { 5L, 6L, 7L }, new List<bool>() { true, false, true });
            Deserialize.Conversions.Clear();
            var L = new Tomlet.Models.TomlArray()
            {
                r.L[0],
                r.L[1],
                r.L[2]
            };
            var B = new Tomlet.Models.TomlArray()
            {
                r.B[0],
                r.B[1],
                r.B[2]
            };

            var tt = Tomlet.Models.TomlDocument.CreateEmpty();
            tt.PutValue("L", L);
            tt.PutValue("B", B);
            //var arr = TomlDotNet.Toml.ConvertArray(a);
            var s = tt.SerializedValue;
            string fname = "Array.toml";
            System.IO.File.WriteAllText(fname, s);
            var rIn = Deserialize.FromFile<HomoArray>(fname);
            Assert.IsFalse((from el in rIn.L.Zip(r.L) where el.First != el.Second select 0).Any());
            Assert.IsFalse((from el in rIn.B.Zip(r.B) where el.First != el.Second select 0).Any());
            ;
        }

        [TestMethod]
        public void HeteroArrayTest()
        {
            var dIn = new HeteroArray(new List<object>() { 5L, true });
            TomlArray A = new();
            //A.Add(dIn.A[0]); // broken: because it causes TOmlet to write '{}', (i.e. an empty inline table) to the file
            A.Add((long)dIn.A[0]);
            A.Add((bool)dIn.A[1]);

            var tt = TomlDocument.CreateEmpty();
            tt.PutValue("A", A);

            string fname = "ArrayHetero.toml";
            System.IO.File.WriteAllText(fname, tt.SerializedValue);
            ;
            var dOut = Deserialize.FromFile<HeteroArray>(fname);
            Assert.IsFalse((from el in dIn.A.Zip(dOut.A) where !UnBoxCompare(el.First, el.Second) select 0).Any());

        }
        /// <summary>
        /// Unboxes an object and see if its ocntents are the same value-wise. Only works for a few types
        /// </summary>
        /// <param name="o1"></param>
        /// <param name="o2"></param>
        /// <returns></returns>
        static bool UnBoxCompare(object o1, object o2)
        {
            if (o1.GetType() != o2.GetType()) return false;
            if (o1 is IComparable c1 && o2 is IComparable c2)
                return c1.CompareTo(c2)==0;
            return false;
            //return o1 switch
            //{
            //    long l => l == (long)o2,
            //    string s => s == (string)o2,
            //    bool b => b == (bool)o2,
            //    _ => throw new NotImplementedException(),
            //};
        }
    }
}
