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

            Deserialize.Conversions.Clear();
            Deserialize.AddNumericConversions();

            var cOut = Deserialize.FromToml<Conv>(tt); // attempt extaction full clas data, requires conversions
            Assert.IsTrue(cIn == cOut);
        }
        public static bool Same<T>(IEnumerable<T> l1, IEnumerable<T> l2) where T : IEquatable<T>
            => (from el in l1.Zip(l2) where el.First.Equals(el.Second) select 0).Any();


        [TestMethod]
        public void ConvertersFromFile()
        {
            Deserialize.Conversions.Clear();
            Deserialize.AddNumericConversions();

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
        public void OptionalAll()
        {
            AllOptional cIn = new();
            var cOut = Deserialize.FromFile<AllOptional>("AllOptional.toml");
            Assert.IsTrue(cIn == cOut);
            ;
        }

        [TestMethod]
        public void HomoArrayTest()
        {
            Deserialize.Conversions.Clear();
            Deserialize.AddNumericConversions();

            var r = new HomoArray(new() { 5L, 6L, 7L }, new List<bool>() { true, false, true }, new() { 8, 9, 10 });
            var L = new Tomlet.Models.TomlArray()
            {
                r.L[0], r.L[1], r.L[2]
            };
            var B = new Tomlet.Models.TomlArray()
            {
                r.B[0], r.B[1], r.B[2]
            };
            var I = new TomlArray()
            {
                r.I[0], r.I[1], r.I[2]
            };

            var tt = Tomlet.Models.TomlDocument.CreateEmpty();
            tt.PutValue("L", L);
            tt.PutValue("B", B);
            tt.PutValue("I", I);
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
        }

        [TestMethod]
        public void ArrayOfTables()
        {
            var fname = "ArrayOfTables.toml";
            var tt = Tomlet.TomlParser.ParseFile(fname);
            var dOut = Deserialize.FromFile<ArrayOfTables>(fname);
            Assert.IsTrue(tt.GetArray("A").Count == dOut.A.Count);
        }
        
        [TestMethod]
        public void FromEnum()
        {
            var dIn = new EnumTypes(Test.Yes, Test.No, Test.Maybe);
            string fname = "Enum.toml";
            var dOut = Deserialize.FromFile<EnumTypes>(fname);
            Assert.IsTrue(dIn == dOut);
            
            try
            {
                var fname2 = "EnumError.toml";
                var dOut2 = Deserialize.FromFile<EnumTypes>(fname2);
                
            }
            catch (AggregateException ex) 
            {
                // expected
                foreach (var e in ex.InnerExceptions) 
                    Console.WriteLine(e);
                return;
            }
            throw new Exception("Should have thrown");
        }


        public record ToVec2(Vector2 V);
        [TestMethod]
        public void ConvertIEnumerable()
        {
            Deserialize.Conversions.Clear();
            Deserialize.AddNumericConversions();
            Deserialize.Conversions.Add((from: typeof(IEnumerable<double>), to: typeof(Vector2)), (ie) => Vector2.Create((IEnumerable<double>)ie));

            { // shuold pass
                var V = new TomlArray()
                {
                    5.0, 6.0,
                };

                var tt = Tomlet.Models.TomlDocument.CreateEmpty();
                tt.PutValue("V", V);

                var v = Deserialize.FromToml<ToVec2>(tt);
            }

            { // should pass (but requires conversions fro mint->double
                var V = new TomlArray()
                {
                    1, 2,
                };

                var tt = Tomlet.Models.TomlDocument.CreateEmpty();
                tt.PutValue("V", V);

                var v = Deserialize.FromToml<ToVec2>(tt);
                Assert.IsTrue(((TomlLong)V[0]).Value == v.V.X && ((TomlLong)V[1]).Value == v.V.Y);
            }

            try 
            {
                var V = new TomlArray()
                {
                    5.0, 6.0, 7.0
                };

                var tt = Tomlet.Models.TomlDocument.CreateEmpty();
                tt.PutValue("V", V);
                var v = Deserialize.FromToml<ToVec2>(tt);
            }
            catch (AggregateException) { return; }
            throw new Exception("Should have thrown");
        }

        [TestMethod]
        public void InlineTable()
        {
            string fname = "InlineTable.toml";
            var t = Deserialize.FromFile<ContainsVec2>(fname); // is OK
            ;
        }

        [TestMethod]
        public void ValueTypeTest()
        {
            Deserialize.Conversions.Clear();
            Deserialize.AddNumericConversions();

            System.Numerics.Vector3 cIn = new() ;// System.Numerics.Vector3(1f, 2f, 3f);
            cIn.X = 1f; cIn.Y = 2f; cIn.Z = 3f;
            Tomlet.Models.TomlTable tt = new();
            tt.Put("X", cIn.X);
            tt.Put("Y", cIn.Y);
            tt.Put("Z", cIn.Z);
            // this shoul duse the valuetype default constructor, then fill its fields from the toml
            var cOut = Deserialize.FromToml<System.Numerics.Vector3>(tt);
            Assert.IsTrue(cIn == cOut);

            TomlTable tt2 = new();
            tt2.Put("x", cIn.X);
            tt2.Put("y", cIn.Y);
            tt2.Put("z", cIn.Z);
            // this one should use the constructor
            var cOut2 = Deserialize.FromToml<System.Numerics.Vector3>(tt2);
            Assert.IsTrue( cIn == cOut2);
        }

        public record ContainsVec2(System.Numerics.Vector2 V);
    }
}
