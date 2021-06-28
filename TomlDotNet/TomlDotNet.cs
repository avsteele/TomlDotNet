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

        private static object FromToml(TomlTable tt, Type t, bool allowNullFillIfMissing)
        {
            // TODO: assuming just one for now
            var constructor = SelectConstructor(t);
            var param_list = constructor.GetParameters();

            var params_ = new object?[param_list.Length];
            foreach (var (v, idx) in param_list.Select((ParameterInfo v, int idx) => (v, idx)))
            {
                params_[idx] = GetObj(tt, v, allowNullFillIfMissing);
            }
            return Convert.ChangeType(constructor.Invoke(params_), t);
        }

        private static object? GetObj(TomlTable tt, ParameterInfo p, bool allowNullFillIfMissing = false)
        {
            if(tt.ContainsKey(p.Name ?? throw new ArgumentException($"{nameof(p)}.name must not be null", nameof(p))))
            {
                var value = tt.GetValue(p.Name);
                return FromToml(value, p.ParameterType, allowNullFillIfMissing);
            }
            else
            {
                if (!allowNullFillIfMissing)
                    throw new InvalidOperationException($"No value for key {p.Name} found in Toml data, and null not allowed by user options");
                if (NullCompatability.IsNullable(p)) return null;
                throw new InvalidOperationException($"No value for key {p.Name} found in Toml data, and null not allowed by type");
            }
        }

        private static object FromToml(TomlValue value, Type type, bool allowNullFillIfMissing)
            => value switch
            {
                TomlString s => type switch 
                    {
                        Type t when t == typeof(string) => FromTomlBase(s),
                        _ => throw new NotImplementedException(),
                    },
                TomlLong i => type switch
                    {
                        Type t when t == typeof(long) => FromTomlBase(i),
                        // look for converter
                        //Type t when t == typeof(int) => Convert.ToInt32(i.Value),
                        _ => Conversions.ContainsKey((from: typeof(long), to: type))
                        ? Conversions[(from: typeof(long), to: type)](i.Value)
                        : throw new InvalidCastException($"long->{type}"),
                    },
                TomlBoolean b => FromTomlBase(b),                
                TomlDouble d => type switch
                {
                    Type t when t==typeof(double) => FromTomlBase(d),
                    _ => Conversions.ContainsKey((from: typeof(double), to: type))
                    ? Conversions[(from: typeof(double), to: type)](d.Value)
                    : throw new InvalidCastException($"double->{type}"),
                },
                TomlArray a => FromTomlBase(a),
                TomlLocalDateTime ldt => FromTomlBase(ldt),
                TomlOffsetDateTime odt => FromTomlBase(odt),
                TomlTable t => FromToml(t, type, allowNullFillIfMissing),
                null => throw new ArgumentNullException(nameof(value)),
                _ => throw new NotImplementedException(type.ToString()),
            };

        private static object FromTomlBase(TomlValue value)
            => value switch
            {
                TomlString s => s.Value,
                TomlLong i => i.Value,
                TomlBoolean b => b.Value,
                TomlDouble d => d.Value,
                TomlArray a => FromTomlArray(a),
                TomlLocalDateTime ldt => ldt.Value,
                TomlOffsetDateTime odt => odt.Value,
                TomlTable => throw new NotSupportedException("Array of tables not supported"),
                _ => throw new InvalidOperationException("Only TomlString, long ,bool, double, or array (of obj) allowed")
            };

        public static List<object> FromTomlArray(TomlArray a)
            => new(a.AsEnumerable().Select((v,_)=>FromTomlBase(v)));


        public static void RecordToTomlFile<T>(T data, string filename) where T:notnull
        {
            var str = RecordToTomlString(data);
            System.IO.File.WriteAllText(filename, str);
        }

        public static string RecordToTomlString<T>(T data) where T : notnull
        {
            var table = ToToml(data, typeof(T));
            return table.SerializeNonInlineTable(null, false);
        }

        /// <summary>
        /// Converts a CLR object into a Tomlet TomlTable.   It looks for a constructor uses this to constrct the object.  
        /// The parameter name in the constructor should match the property name ot be extracted. This is the case for e.g.
        /// default-made record constructors 'public record Data(int I, string S...);'
        /// </summary>
        /// <param name="data"></param>
        /// <param name="t"></param>
        /// <returns></returns>
        public static TomlTable ToToml(object data, Type t)
        {
            if (data is null) throw new ArgumentNullException(nameof(data));
            var tt = new Tomlet.Models.TomlTable();

            //iterate over constructor
            var constructor = SelectConstructor(t);
            var param_list = constructor.GetParameters();

            foreach (var p in param_list)
            {
                if (p.ParameterType.Equals(t)) 
                    throw new InvalidOperationException("Cannot types with constructors containing an element of thier own type");
                if (p.Name is null) throw new InvalidOperationException($"Constructor parameter name {p.Name} is null?");
                var obj = GetPropValue(data, p.Name);
                if (obj is null) throw new InvalidOperationException($"Unexpected property {p.Name}'s value not found on object");
                var tomlValue = ToTomlBase(obj);
                if (tomlValue is not null)
                    tt.PutValue(p.Name, tomlValue);
                else
                    ToToml(obj, p.ParameterType);
            }
            return tt;
        }

        public static TomlValue? ToTomlBase(object data) => data switch
        {
            bool b => TomlBoolean.ValueOf(b),
            double _ or float _ => new TomlDouble(Convert.ToDouble(data)),
            long _ or int _ or uint _ or short _ or ushort _ or byte _ => new TomlLong(Convert.ToInt64(data)),
            string s => new TomlString(s),
            DateTime dt => new TomlLocalDateTime(dt),
            DateTimeOffset dto => new TomlOffsetDateTime(dto),
            _ => throw new InvalidOperationException($"No known conversion from {data.GetType()} to Toml"),
        };

        public static object? GetPropValue(object src, string propName)
        {
            return src.GetType().GetProperty(propName)?.GetValue(src, null);
        }

        /// <summary>
        /// Selects a constructor on type t that is used for serialization/deserialization
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static ConstructorInfo SelectConstructor(Type t)
        {
            return t.GetConstructors(BindingFlags.Public | BindingFlags.Instance)[0];
        }
    }
}