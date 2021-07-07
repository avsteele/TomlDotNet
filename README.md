# TomlDotNet

Simple serialization of CLR records to TOML and the reverse. Uses TOMLET (https://github.com/SamboyCoding/Tomlet) to parse TOML.

WARNING: this works, but is a work-in-progress. The API may change at any time.

## Capabilities

### De-serialization (TOML-> C# record)

```TOML
# in file Example.toml: 
L = 5		# toml long
D = 0.123	# toml double
S = "hello"	# toml string
B = true	# toml bool
```

```csharp
public record Data(long L, double D, string S, bool B);

// ...

var dIn = TomlDotNet.Deserialize.FromFile<Data>("Example.toml");

Assert.IsTrue((dIn.L == 5) && (dIn.D == 0.123) && (dIn.S=="hello") && (dIn.B == true));
```

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

C# Records map quite well to TOML data. By using the the TOML data to populate the parameters of a constructor we can ensure a well constructed object. 

One key improvement in this method is that we  can deserialize without exposing public fields or properties of our objects. The default immutability of records is also a good fit for representation of on-disk toml data.

Serialization is less straightforward, but if you use default-constructed records

<https://docs.microsoft.com/en-us/dotnet/csharp/whats-new/tutorials/records>

## TODO

- [X] Basic De-serialization from TOML file to C# record
- [X] Basic Serialization from TOML file to a C# record
- [X] Basic type conversions (ie import TOML (I64) to C# Int etc...)
- [X] Constructor selection
- [X] Optional TOML toml fields
- [X] Array of Tables support
- [ ] Inline Array of tables support
- [X] Conversion from Toml Tables to any type with an `IEnumerable` constructor
