# TomlDotNet
Simple serialization of CLR records to TOML and the reverse. Uses TOMLET (https://github.com/SamboyCoding/Tomlet) for parse the TOML.

## Capabilities

### Deserialization (TOML-> C# record)
```TOML
# in file Example.toml: 
#    public record Data(long L, double D, string S, bool B);
L = 5
D = 0.123
S = "hello"
B = true
```

```csharp
public record Data(long L, double D, string S, bool B);

// ...

var dIn = TomlDotNet.Deserialize.FromFile<Data>("Example.toml");

Assert.IsTrue((dIn.L == 5) && (dIn.D == 0.123) && (dIn.S=="hello") && (dIn.B == true));
```

## TODO

- [X] Basic Deserialization from TOML file to C# record
- [X] Basic Serialization from TOML file to a C# record
- [ ] Basic type conversions
- [ ] Refinements to conversion API


