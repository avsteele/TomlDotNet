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
        static Deserialize()
        {
            // add identify conversions
            BuiltInConversions.Add((typeof(string), typeof(string)), s => s);
            BuiltInConversions.Add((typeof(long), typeof(long)), s => s);
            BuiltInConversions.Add((typeof(bool), typeof(bool)), s => s);
            BuiltInConversions.Add((typeof(double), typeof(double)), s => s);
            BuiltInConversions.Add((typeof(DateTime), typeof(DateTime)), s => s);
            BuiltInConversions.Add((typeof(DateTimeOffset), typeof(DateTimeOffset)), s => s);

        }

        private static Dictionary<(Type from, Type to), Func<object, object>> BuiltInConversions { get; set; } = new();

        public static Dictionary<(Type from, Type to), Func<object, object>> Conversions { get; private set; } = new();

        /// <summary>
        /// Constructs an instance of type T from the contents of the TOML file filePath
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="filePath">realtive or absolute path to a TOML file</param>
        /// <param name="allowNullFillIfMissing"></param>
        /// <returns></returns>
        public static T FromFile<T>(string filePath) where T : class
            => FromString<T>(System.IO.File.ReadAllText(filePath));

        /// <summary>
        /// As FromFile, but the TOML file contents are passed directly as a string
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tomlFileContents"></param>
        /// <param name="allowNullFillIfMissing"></param>
        /// <returns></returns>
        public static T FromString<T>(string tomlFileContents) where T : class
        {
            var parser = new Tomlet.TomlParser();
            return FromToml<T>(parser.Parse(tomlFileContents));
        }

        public static T FromToml<T>(TomlTable tt) where T : class
            => (T)FromToml(tt, typeof(T));

        private static object FromToml(TomlTable tt, Type t)
        {
            foreach(var c in ConstructorTryOrder(t,tt))
            {
                try
                { return FromToml(tt, t, c); }
                catch {}
            }
            throw new InvalidOperationException($"No valid constructor found able to convert Toml to type {t}");
        }

        public static object FromToml(TomlTable tt, Type t, ConstructorInfo c)
        {
            var param_list = c.GetParameters();

            var params_ = new object?[param_list.Length];
            foreach (var (v, idx) in param_list.Select((ParameterInfo v, int idx) => (v, idx)))
            {
                params_[idx] = GetObj(tt, v);
            }
            return Convert.ChangeType(c.Invoke(params_), t);
        }

        private static object? GetObj(TomlTable tt, ParameterInfo p)
        {
            if(p.Name is null) throw new ArgumentException($"{nameof(p)}.name must not be null", nameof(p));
            if( tt.ContainsKey(p.Name))
            {
                var value = tt.GetValue(p.Name);
                return FromToml(value, p.ParameterType);
            }
            else
            {
                if (p.IsOptional) return p.DefaultValue;
                throw new InvalidOperationException($"no element in TOML found to match non-optional {p.Name}");
            }
        }

        /// <summary>
        /// Toml value to CLR type 'type'
        /// </summary>
        /// <param name="value"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private static object FromToml(TomlValue from, Type toType)
            => from switch
            {
                null => throw new ArgumentNullException(nameof(from)),
                TomlArray a => TryMakeHomogeneous(a, toType.GenericTypeArguments[0]),
                TomlTable t => FromToml(t, toType),
                // rest should be singletons
                _ => DoConversion(ExtractValue(from), toType), //throw new NotImplementedException(type.ToString()),
            };

        private static object DoConversion(object o, Type type)
            // try builtIn dict of conversions, then fallback to user defined
            => BuiltInConversions.ContainsKey((from: o.GetType(), to: type))
                ? BuiltInConversions[(from: o.GetType(), to: type)](o)
                : Conversions.ContainsKey((from: o.GetType(), to: type))
                ? Conversions[(from: o.GetType(), to: type)](o)
                : throw new InvalidCastException($"{o.GetType()}->{type}");

        private static object ExtractValue(TomlValue v)
        => v switch
        {
            TomlString s => s.Value,
            TomlLong i => i.Value,
            TomlBoolean b => b.Value,
            TomlDouble d => d.Value,
            TomlLocalDateTime ldt => ldt.Value,
            TomlOffsetDateTime odt => odt.Value,
            _ => throw new InvalidOperationException("Only defined for single-value types"),
        };

        //private static object FromTomlBase(TomlValue value)
        //    => value switch
        //    {
        //        TomlString s => s.Value,
        //        TomlLong i => i.Value,
        //        TomlBoolean b => b.Value,
        //        TomlDouble d => d.Value,
        //        TomlArray a => FromTomlArray(a),
        //        TomlLocalDateTime ldt => ldt.Value,
        //        TomlOffsetDateTime odt => odt.Value,
        //        TomlTable => throw new NotSupportedException("Array of tables not supported"),
        //        _ => throw new InvalidOperationException("Only TomlString, long ,bool, double, or array (of obj) allowed")
        //    };

        //public static List<object> FromTomlArray(TomlArray a)
        //    => new(a.AsEnumerable().Select((v,_)=>FromTomlBase(v)));

        /// <summary>
        /// Construct a homegeneous list from a toml array
        /// </summary>
        /// <param name="t">list element type </param>
        /// <param name="a"></param>
        /// <returns></returns>
        private static object TryMakeHomogeneous(TomlArray a, Type t)
        {
            //if (t == typeof(List<object>)) return l;
            
            var listType = typeof(List<>);
            var constructedListType = listType.MakeGenericType(t);

            var instance = Activator.CreateInstance(constructedListType);
            if (instance is null) throw new InvalidOperationException("INstance of array is null?");
            var inst2 = (System.Collections.IList)instance!;
            foreach(var el in a.AsEnumerable())
            {
                inst2.Add(ExtractValue(el));
            }
            return inst2;
        }
        /// <summary>
        /// Filters public, instance constructors of type t on thier number of parameters.  
        /// If a Tomlable is provided it filters other those that do not have 
        /// </summary>
        /// <param name="t"></param>
        /// <param name="tt"></param>
        /// <returns></returns>
        public static List<ConstructorInfo> ConstructorTryOrder(Type t, TomlTable? tt)
        {
            var cs = t.GetConstructors(BindingFlags.Public | BindingFlags.Instance).ToList();
            // sort on # params
            cs.Sort((c1, c2) => c1.GetParameters().Length.CompareTo(c2.GetParameters().Length));
            if (tt is null) return cs;            
            // next line filter constructors with more required params than keys in the tomltable
            //     this is just a performance enhancement really, so tt is optional
            var l = (from c in cs where NumberRequiredParams(c) <= tt.Keys.Count select c).ToList();
            return l;
        }

        public static int NumberRequiredParams(ConstructorInfo c)
            => (from p in c.GetParameters() where !p.IsOptional select p).Count();
    }
}