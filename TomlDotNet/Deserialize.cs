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
            // add 'identity' conversions
            BuiltInConversions.Add((typeof(string), typeof(string)), s => s);
            BuiltInConversions.Add((typeof(long), typeof(long)), l => l);
            BuiltInConversions.Add((typeof(bool), typeof(bool)), b => b);
            BuiltInConversions.Add((typeof(double), typeof(double)), d => d);
            BuiltInConversions.Add((typeof(DateTime), typeof(DateTime)), dt => dt);
            BuiltInConversions.Add((typeof(DateTimeOffset), typeof(DateTimeOffset)), dto => dto);

            // these are used for filling elements of hereteogeneous arrays
            BuiltInConversions.Add((typeof(string), typeof(object)), o => o);
            BuiltInConversions.Add((typeof(long), typeof(object)), o => o);
            BuiltInConversions.Add((typeof(bool), typeof(object)), o => o);
            BuiltInConversions.Add((typeof(double), typeof(object)), o => o);
            BuiltInConversions.Add((typeof(DateTime), typeof(object)), o => o);
            BuiltInConversions.Add((typeof(DateTimeOffset), typeof(object)), o => o);

        }

        private static Dictionary<(Type from, Type to), Func<object, object>> BuiltInConversions { get; set; } = new();

        public static Dictionary<(Type from, Type to), Func<object, object>> Conversions { get; private set; } = new();

        /// <summary>
        /// Constructs an instance of type T from the contents of the TOML file filePath
        /// </summary>
        /// <typeparam name="T">class constraint required because value types can always be default constructed (they match any toml file.)</typeparam>
        /// <param name="filePath">realtive or absolute path to a TOML file</param>
        /// <returns></returns>
        public static T FromFile<T>(string filePath) where T : class
            => FromString<T>(System.IO.File.ReadAllText(filePath));

        /// <summary>
        /// As FromFile, but the TOML file contents are passed directly as a string
        /// </summary>
        /// <typeparam name="T">See Fromfile</typeparam>
        /// <param name="tomlFileContents"></param>
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
                TomlArray a => BuildFromArray(a,toType),
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

        /// <summary>
        /// Build toType 
        /// </summary>
        /// <param name="fromToml"></param>
        /// <param name="toType"></param>
        /// <returns></returns>
        private static object BuildFromArray(TomlArray fromToml, Type toType)
        {
            /// 1) find a constructor on ToType with an single enumerable <T> parmeter, wher T is toType.GenericTypeParameter[0]
            /// 2) construct an empty List<T>
            /// 3) convert each element of the TomlArray to type T, then add each to the List<T>
            /// 4) use this list as an arg to the constructor from 1) above
            
            var c = BuildFromEnumerable(toType);
            if (c is null) throw new InvalidOperationException("No constructor with ennumerable parameter");

            var listType = typeof(List<>);
            var elementType = toType.GenericTypeArguments[0]; // for heterog this might be 'object'
            var constructedListType = listType.MakeGenericType(elementType);
            //IEnumerable<object> prm = from el in fromToml select DoConversion(ExtractValue(el), elementType);
            IEnumerable<object> prm = from el in fromToml select FromToml(el,elementType);

            // can't figure out how to construct the list directly from prm
            var instance = (System.Collections.IList)Activator.CreateInstance(constructedListType);
            if (instance is null) throw new InvalidOperationException($"Failed to construct List<{elementType}> to construct {toType}");
            foreach( var p in prm)
                instance.Add(p);

            return c.Invoke(new object[] { instance });
        }

        /// <summary>
        /// Looks for a consttructor taking an Ennumerable<T> where T is the 
        /// single GenericTypeArgument of Type t (the parameter)
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static ConstructorInfo? BuildFromEnumerable(Type t)
        {
            if (t.GenericTypeArguments.Length != 1) return null;
            var enumType = typeof(IEnumerable<>);
            var constructedEnumType = enumType.MakeGenericType(t.GenericTypeArguments[0]);

            var cs = t.GetConstructors();
            foreach(var c in cs)
            {
                ParameterInfo[] ps = c.GetParameters();
                if (ps.Length != 1) continue;
                if (ps[0].ParameterType == constructedEnumType) return c;
            }
            return null; //so suitable constructire found
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