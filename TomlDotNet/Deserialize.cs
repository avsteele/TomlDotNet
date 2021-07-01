using System;
using System.Reflection;
using System.Linq;
using Tomlet.Models;
using System.Collections.Generic;


namespace TomlDotNet
{

    /// https://github.com/SamboyCoding/Tomlet
    /// https://github.com/toml-lang/toml/blob/master/toml.md
    /// https://github.com/paiden/Nett
    //
    /// <summary>
    /// Method facilitation conversion from TOML data to CLR objects.
    /// </summary>
    public static class Deserialize
    {
        public static Dictionary<(Type from, Type to), Func<object, object>> Conversions { get; private set; } = new();

        /// <summary>
        /// Constructs an instance of type T from the contents of the TOML file filePath
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="filePath">realtive or absolute path to a TOML file</param>
        /// <param name="allowNullFillIfMissing"></param>
        /// <returns></returns>
        public static T FromFile<T>(string filePath, bool allowNullFillIfMissing = false) where T : class
            => FromString<T>(System.IO.File.ReadAllText(filePath), allowNullFillIfMissing);

        /// <summary>
        /// As FromFile, but the TOML file contents are passed directly as a string
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tomlFileContents"></param>
        /// <param name="allowNullFillIfMissing"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Selects a constructor on type t that is used for serialization/deserialization
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static ConstructorInfo SelectConstructor(Type t)
        {
            var cs = t.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            if (cs.Length == 1) return cs[0];
            throw new InvalidOperationException($"Cannot select constructor to use as (de)serialization guide. Found more than 1 type {t.FullName}");
        }

        /// <summary>
        /// Filteres public, instance constructors of type t that have more paramers than keys of the toml table.
        /// </summary>
        /// <param name="t"></param>
        /// <param name="tt"></param>
        /// <returns></returns>
        public static List<ConstructorInfo> ConstructorTryOrder(Type t, TomlTable tt)
        {
            var cs = t.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            // next line ensures there are enough keys to fill the # of reuired (non-optional) parameters
            var l = (from c in cs where NumberRequiredParams(c) <= tt.Keys.Count select c).ToList();
            //var l = cs.ToList();
            l.Sort((c1, c2) => c1.GetParameters().Length.CompareTo(c2.GetParameters().Length));
            return l;
        }

        public static int NumberRequiredParams(ConstructorInfo c)
            => (from p in c.GetParameters() where !p.IsOptional select p).Count();
    }
}