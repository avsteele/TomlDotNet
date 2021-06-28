using System;
using System.Reflection;
using System.Linq;
using Tomlet.Models;
using System.Collections.Generic;


namespace TomlDotNet
{

    //public delegate object Converter(TomlValue value, Type targetType);
    //public delegate T ValueTypeConverter<T, U>(U from) where T : struct where U : struct;
    /// https://github.com/SamboyCoding/Tomlet
    /// https://github.com/toml-lang/toml/blob/master/toml.md
    /// https://github.com/paiden/Nett
    //
    /// <summary>
    /// 
    /// </summary>
    public static class Toml
    {
        public static Dictionary<(Type from, Type to), Func<object, object>> Conversions { get; private set; } = new();

        public static T FromFile<T>(string filePath, bool allowNullFillIfMissing = false) where T : class
            => FromString<T>(System.IO.File.ReadAllText(filePath), allowNullFillIfMissing);

        public static T FromString<T>(string tomlFileContents, bool allowNullFillIfMissing = false) where T : class
        {
            var parser = new Tomlet.TomlParser();
            return FromToml<T>(parser.Parse(tomlFileContents), allowNullFillIfMissing);
        }

        public static T FromToml<T>(TomlTable tt, bool allowNullFillIfMissing = false) where T : class
            => (T)FromToml(tt, typeof(T), allowNullFillIfMissing);

        private static object FromToml(TomlTable tt, Type type, bool allowNullFillIfMissing)
        {
            // TODO: assuming just one for now
            var constructor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)[0];
            var param_list = constructor.GetParameters();

            var params_ = new object?[param_list.Length];
            foreach (var (v, i) in param_list.Select((v, i) => (v, i)))
            {
                params_[i] = GetObj(tt, v, allowNullFillIfMissing);
            }
            return Convert.ChangeType(constructor.Invoke(params_), type);
        }

        private static object? GetObj(TomlTable tt, ParameterInfo p, bool allowNullFillIfMissing = false)
        {
            if(tt.ContainsKey(p.Name ?? throw new ArgumentException($"{nameof(p)}.name must not be null", nameof(p))))
            {
                var value = tt.GetValue(p.Name);
                return ConvertObj(value, p.ParameterType, allowNullFillIfMissing);
            }
            else
            {
                if (!allowNullFillIfMissing)
                    throw new InvalidOperationException($"No value for key {p.Name} found in Toml data, and null not allowed by user options");
                if (NullCompatability.IsNullable(p)) return null;
                throw new InvalidOperationException($"No value for key {p.Name} found in Toml data, and null not allowed by type");
            }
        }

        private static object ConvertObj(TomlValue value, Type type, bool allowNullFillIfMissing)
            => value switch
            {
                TomlString s => type switch 
                    {
                        Type t when t == typeof(string) => ConvertBaseObj(s),
                        _ => throw new NotImplementedException(),
                    },
                TomlLong i => type switch
                    {
                        Type t when t == typeof(long) => ConvertBaseObj(i),
                        // look for converter
                        //Type t when t == typeof(int) => Convert.ToInt32(i.Value),
                        _ => Conversions.ContainsKey((from: typeof(long), to: type))
                        ? Conversions[(from: typeof(long), to: type)](i.Value)
                        : throw new InvalidCastException($"long->{type}"),
                    },
                TomlBoolean b => ConvertBaseObj(b),                
                TomlDouble d => type switch
                {
                    Type t when t==typeof(double) => ConvertBaseObj(d),
                    _ => Conversions.ContainsKey((from: typeof(double), to: type))
                    ? Conversions[(from: typeof(double), to: type)](d.Value)
                    : throw new InvalidCastException($"double->{type}"),
                },
                TomlArray a => ConvertBaseObj(a),
                TomlLocalDateTime ldt => ConvertBaseObj(ldt),
                TomlOffsetDateTime odt => ConvertBaseObj(odt),
                TomlTable t => FromToml(t, type, allowNullFillIfMissing),
                null => throw new ArgumentNullException(nameof(value)),
                _ => throw new NotImplementedException(type.ToString()),
            };

        private static object ConvertBaseObj(TomlValue value)
            => value switch
            {
                TomlString s => s.Value,
                TomlLong i => i.Value,
                TomlBoolean b => b.Value,
                TomlDouble d => d.Value,
                TomlArray a => ConvertArray(a),
                TomlLocalDateTime ldt => ldt.Value,
                TomlOffsetDateTime odt => odt.Value,
                TomlTable => throw new NotSupportedException("Array of tables not supported"),
                _ => throw new InvalidOperationException("Only TomlString, long ,bool, double, or array (of obj) allowed")
            };

        public static List<object> ConvertArray(TomlArray a)
            => new(a.AsEnumerable().Select((v,_)=>ConvertBaseObj(v)));


        public static string ToTomlString<T>(T data)
        {
            var table = ToToml(data, typeof(T));
            return table.SerializeNonInlineTable(null, false);
        }

        /// <summary>
        /// Converts a CLR object into a Tomlet TomlTable.   It looks for a constructor uses this to constrct the object.  
        /// This is the inverse of the 
        /// </summary>
        /// <param name="data"></param>
        /// <param name="t"></param>
        /// <returns></returns>
        public static TomlTable ToToml(object data, Type t)
        {
            if (data is null) throw new ArgumentNullException(nameof(data));
            var tt = new Tomlet.Models.TomlTable();
            
            //iterate over constructor
            var constructor = t.GetConstructors(BindingFlags.Public | BindingFlags.Instance)[0];
            var param_list = constructor.GetParameters();

            foreach (var p in param_list)
            {
                if (p.ParameterType.Equals(t)) 
                    throw new InvalidOperationException("Cannot types with constructors containing an element of thier own type");
                var obj = GetPropValue(data, p.Name);
                if (IsBasicTomlType(p.ParameterType))
                {
                    tt.PutValue(p.Name, ToTomlBase(obj));
                }
                else
                    ToToml(obj, p.ParameterType);
            }
            return tt;
        }

        /// <summary>
        /// Tests whether there is a known conversion between a passed CLR type and a TOML type
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static bool IsBasicTomlType(Type t)
        {
            // TODO incorporate use of converters
            var baseTypes = new List<Type>() 
            { 
                typeof(long), typeof(int), typeof(uint), typeof(short), typeof(ushort), typeof(byte),// never error conversions
                typeof(string), 
                typeof(bool), 
                typeof(double), typeof(float),
                typeof(DateTime), 
                typeof(DateTimeOffset)};
            return (from b in baseTypes where t.Equals(b) select b).Any();
        }

        public static TomlValue ToTomlBase(object data)
            => data switch
            {
                bool b => Tomlet.Models.TomlBoolean.ValueOf(b), // no idea why this is differnt from other in the library
                double d => new Tomlet.Models.TomlDouble(d),
                float f => new Tomlet.Models.TomlDouble(f),
                DateTime dt => new Tomlet.Models.TomlLocalDateTime(dt),
                DateTimeOffset dto => new Tomlet.Models.TomlOffsetDateTime(dto),
                long l => new Tomlet.Models.TomlLong(l),
                int i => new Tomlet.Models.TomlLong(i),
                uint ui => new Tomlet.Models.TomlLong(ui),
                short s => new Tomlet.Models.TomlLong(s),
                ushort us => new Tomlet.Models.TomlLong(us),
                byte b => new Tomlet.Models.TomlLong(b),
                string s => new Tomlet.Models.TomlString(s),
                _ => throw new InvalidOperationException($"To known conversion from {data.GetType()} to Toml")
            };

        public static object? GetPropValue(object src, string propName)
        {
            return src.GetType().GetProperty(propName)?.GetValue(src, null);
        }
    }
}