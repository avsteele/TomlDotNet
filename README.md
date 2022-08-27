# TomlDotNet

Simple serialization of C# types to TOML and the reverse.

Uses TOMLET (https://github.com/SamboyCoding/Tomlet) as a dependency to 
parse TOML.  However, no TOMLET types are actually exposed.

WARNING: this works, but is a work-in-progress. The API may change at any time.

## Capabilities

### De-serialization (TOML-> C# record)

```TOML
# in file Example.toml: 
L = 5		# toml long
D = 0.123	# toml double
S = "hello"	# toml string
B = true	# toml bool
E = "Yes"   # toml string to enum
```

```csharp
public enum EnumType { Yes, No, Maybe };
public record Data(long L, double D, string S, bool B, EnumType E);

// ...

var dIn = TomlDotNet.Deserialize.FromFile<Data>("Example.toml");

Assert.IsTrue((dIn.L == 5) && (dIn.D == 0.123) && (dIn.S=="hello") && (dIn.B == true) && (dIn.E == EnumType.Yes);
```

COnversions from a 

#### Method

The library will attempt to construct an object with one of its public contructors, using 
the TOML to fill the constructor parameters. The paramters can in turn be recursively constructed
using sub-tables in the TOML.

If successful, the constructed object is returned.  

If only a parameterles constructor can be used, then the public set-or-init properties and 
public fields will be set using the TOML.

This method should work well for classes, structs and records.

Cosntructor parameters, properties, fields implementing `IEnumberable<T>` will match with any TOML homgeneous array.
`IEnumerable<object>` can be used to match hetergenous TOML arrays.

### Serialization

```csharp
public record Data(long L, double D, string S, bool B);

// ...
var data = new Data(1L, 2.2, "hi", true);
string str = TomlDotNet.Serialize.ToString(data);
// or can use ToFile(data, fileName)
```

All properties public with  public `set` or `init` are serialized, unless the 
property has the `[field:NonSerialized]` attribute applied.

Public Fields are serialized, unless the field has the `[NonSerialized]` attribute.

In the case of `record`s the public property names match the constructor 
parameter names by default. So Serialization followed by deserialization should 
always proceed via the constructor.

### Support for conversions

```TOML
# in Conversions.toml
L = 5
I = 6       # toml long, will be imported as int
UI = 7
UL = 8
LtoD = 9    # toml long will import and convert to double
LtoF = 10
D = 12.34
F = 45.67
```

TOML integers map to c# longs. Conversions to other integral types can be 
supported via adding conversions.

```csharp
public record Conv(long L, int I, uint UI, ulong UL, double LtoD, float LtoF, double D, float F);
//...
import TomlDotNet;

Deserialize.Conversions.Clear();
// add conversion toml int (long) to int
Deserialize.Conversions.Add((typeof(long), typeof(int)), (i) => Convert.ToInt32((long)i));
// toml long to c# uint
Deserialize.Conversions.Add((typeof(long), typeof(uint)), (i) => Convert.ToUInt32((long)i));
// toml long to c# ulong
Deserialize.Conversions.Add((typeof(long), typeof(ulong)), (i) => Convert.ToUInt64((long)i));
// toml long to c# single
Deserialize.Conversions.Add((typeof(long), typeof(float)), (i) => Convert.ToSingle((long)i));
// otml long to c# double
Deserialize.Conversions.Add((typeof(long), typeof(double)), (i) => Convert.ToDouble((long)i));
// toml double to c# float
Deserialize.Conversions.Add((typeof(double), typeof(float)), (d) => Convert.ToSingle((double)d));

var cOut = TomlDotNet.Deserialize.FromFile<Conv>("Conversions.toml"); 
```

These and other numeric conversions can simply be included with a call to `Deserialize.AddNumericConversions()`

Conversion from many TOML type can be handled by adding converters. 
Constrctors for an object are alwasy checked first.

### Optional Fields

```TOML
# in optional.toml
L = 5
D = 6.6
```

```csharp
public record Optional( long L, double D, string S="hi", bool B = true);

// we can construct an 'Optional' record even if the TOML is missing a field as 
//    long as that field is optional
var cOut = TomlDotNet.Deserialize.FromFile<Optional>("Optional.toml");
```

Many additional examples of usage can be found in the included test package (`TomlDotNet.Tests`)

## Methods / Why

C# `record`s map nicely to TOML data. By using the the TOML data to populate
the parameters of a constructor we can ensure a well constructed object.

Wile support for records was a motivation, the library should work fine with 
`class`es and `struct` as well

One key improvement in this method is that we can deserialize without exposing public fields
 or properties of our objects. The default immutability of records is also a good fit for 
 representation of on-disk toml data.

Serialization is less straightforward, but if you use default-constructed records

<https://docs.microsoft.com/en-us/dotnet/csharp/whats-new/tutorials/records>

## Features

- [X] Basic De-serialization from TOML file to C# record
- [X] Basic Serialization from TOML file to a C# record
- [X] Basic type conversions (ie import TOML (I64) to C# Int etc...)
- [X] Constructor selection
- [X] Optional paramters for deserialization, optional TOML
- [X] Array of Tables support
- [X] De-serialization from Toml Tables to any type with an `IEnumerable` constructor or converter ass to the dictionary
- [X] Serialization from IEnnumerbale object to a TomlArray
- [ ] Inline Array of tables support
