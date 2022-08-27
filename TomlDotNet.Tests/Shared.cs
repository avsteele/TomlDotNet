using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.CompilerServices;

#pragma warning disable IDE0060 // Remove unused parameter
/// <summary>
/// Many records here use in tests via reflection. No not remove them just because static analysis says theya re unused
/// </summary>
namespace TomlDotNet.Tests
{
    public record Data(long L, double D, string S, bool B);

    public record Many(long L, int I, uint UI, short Sh, ushort US, byte By, double D, float F, string S, bool B);

    public record Nested(Inner I);
    public record Inner(long L);

    public record HeteroArray(List<object> A);
    public record HomoArray(List<long> L, List<bool> B, List<int> I);
    public record ArrayOfTables(List<TableElement> A);
    public record TableElement(long L, bool B);

    public record ArrayOfTables2(List<TableElement2> A);
    public record TableElement2(long L, bool B, string S, double D, List<double> LD);

    public record Conv(long L, int I, uint UI, ulong UL, double LtoD, float LtoF, double D, float F);

    public record DatesTimes(DateTime DT, DateTime DTUtc, DateTimeOffset Dto);

    //public record NullTypes(int? In, string? Sn);
    public record NoNullTypes(string Sn);

    /// <summary>
    /// USed in test Deserialize -> TestConstructorFilter
    /// </summary>
    public class ManyConstructors
    {
        //public ManyConstructors() { }
        static ManyConstructors() { } //should be filtered (is static)
#pragma warning disable IDE0051 // Remove unused private members
        private ManyConstructors(int z, int y, int x) { } //should be filtered (is private)
#pragma warning restore IDE0051 // Remove unused private members
        public ManyConstructors(int i, int j, int k, int l, int m, int n, int o, int p, int q) { } //should be filtered, # req params too large
        public ManyConstructors(int i) { }
        public ManyConstructors(int i, string s) { }
        public ManyConstructors(int i = 5, int j = 10, int k = 15, int l = 20, int m = 25) { }
    }

    public record Optional( long L, double D, string S="hi", bool B = true)
    {
        public Optional(long L, double D, string S, bool B, string S2) 
            : this(L, D, S, B) { throw new Exception("Shouldn't be called"); }
    }

    public record AllOptional(long L = 0, double D = 0.0, string S = "opt");

    public enum Test { Yes, No, Maybe }
    public record EnumTypes( Test One, Test Two, Test Three);

    public class Vector2
    {
        public double X { get; init; }
        public double Y { get; init; }
        static public Vector2 Create(IEnumerable<double> xy)
        {
            if (xy.Count() != 2) throw new ArgumentException("Requires exactly 2 elements", nameof(xy));
            return new Vector2()
            {
                X = xy.First(),
                Y = xy.Last(),
            };
        }
        private Vector2() { }
    }

    public class Derived : Base
    {
        public double A { get; set; } = 5;
    };

    public class Base
    {
        public double B { get; set; } = 6;

        [field: NonSerialized]
        public double C { get; set; } = 7;
    }
}