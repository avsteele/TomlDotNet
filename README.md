# TomlDotNet
Simple serialization of CLR records to TOML and the reverse. Uses TOMLET (https://github.com/SamboyCoding/Tomlet) for parse the TOML.

## Capabilities

### Deserialization (TOML-> C# record)


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

### Cupport for conversions

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


## TODO

- [X] Basic Deserialization from TOML file to C# record
- [X] Basic Serialization from TOML file to a C# record
- [ ] Basic type conversions
- [ ] Refinements to conversion API


