# Johnson Controls Numerics
This repository contains a collection of tools, patterns, methods, and algorithms for solving various 
problems common in our industry - especially in the numerics space. C# is our language of choice, but the 
concepts are applicable to any language and we welcome ports directly within this repoisotry.

- [Units of Measure (C#)](#units-csharp)
	- [Creating Units](#create-units)
	- [Convert Units](#convert-units)
	- [Composing Units](#compose-units)
	- [Syntax Sugar](#syntax-sugar-units)	
	- [Configuration](#config-units)

<div id='units-csharp'/>

## Units of Measure (C#)
Yes, this is yet another units of measure implementation that provides common functionality, but we find that most are unnecessarily rigid
or are lacking features our algorithms rely heavily on. This work focuses on providing a simple, flexible, and extensible framework for 
units of measure that is fully configurable via a json file during startup. 

The primary function of units of measure is to compose and convert between units, including all of their many misnomers/spellings 
such as FT3_PER_MIN or CFM rather than ft^3/min (this implementation allows many to exist together in harmony). The library is not optimized for 
aggressive unit math as it is intended to be used to compute units once per operation and then leverage the conversion factor and offset
for many value combinations thereafter rather than using it to compute something like 10 * m + 7 * m into 17 * m for each operation. 

<div id='create-units'/>

### Creating Units <a name="create-units"/>
The first fundamental purpose of the implementation is to derive a unit of measure from an expression. This expression can be supplied as 
a string, a span of chars, or a span of bytes (for utf8). 

```csharp
//Direct creation
var degF = UnitOfMeasure.Create("degF");
Assert.IsNotNull(degF); 

//ReadOnlySpan<char>(utf16) or ReadOnlySpan<byte>(utf8) supported 
degF = UnitOfMeasure.Create("degF".AsSpan()); 
Assert.IsNotNull(degF);

//Implicit conversions
degF = "degF"; 
Assert.IsNotNull(degF); 

//Attempt creation to handle error path
if (!UnitOfMeasure.TryCreate("degC", out var degC, out var error)) 
    throw new Exception(error);

//Library attempts to guess incorrect spelling (units are naturally case sensitive)
Assert.IsFalse(UnitOfMeasure.TryCreate("degc", out _, out var degcError)); 
Assert.AreEqual("degc: Unrecognized unit expression 'degc' at position 0. Did you mean: degC, degF, degR, delC, deg, Eg, dag, dg, delF, EGy, daGy?", degcError); 
```

<div id='convert-units'/>

### Converting Units
The second fundamental purpose of the implementation is to convert between two units of measure. Once an expression has been parsed, 
leveraging a span-based cache, conversion factors and offsets are obtainable to facilitate conversions.

```csharp
//Different ways to convert units
var (f, o) = degF.GetConversionTo(degC);
Assert.AreEqual(0, 32 * f + o, 1E-9); // 32 degF = 0 degC

if (!degF.TryGetConversionTo(degC, out f, out o))
    Assert.Fail(); 
Assert.AreEqual(0, 32 * f + o, 1E-9);

Assert.IsTrue(degF.IsConvertibleTo(degC));
Assert.IsFalse(degF.IsConvertibleTo("meter")); //Implicit unit of measure conversion (meter)
```

<div id='compose-units'/>

### Composing Units
Units are highly composable, meaning that expressions of configured units are encouraged rather than overspecifying. 
For example, we may choose to define a meter and a second, but acceleration can just be composed as m / s ^ 2! Of course, 
any string can contain these expressions, but the implementation also offers garbage free operators to help combine existing units. 

```csharp
//Compose your own units (underlying garbage-free string combinations via buffers)
var mSq = UnitOfMeasure.Create("m") * "m";
Assert.AreEqual("m*m", mSq.ToString());
Assert.IsTrue(mSq.IsConvertibleTo("m^2"));
Assert.IsTrue(mSq.IsConvertibleTo("(m^(3/2))^(4/3)"));

var m = mSq / "m";
Assert.AreEqual("m*m/(m)", m.ToString());
Assert.IsTrue(m.IsConvertibleTo("m"));

var inch = UnitOfMeasure.Create("in");
Assert.AreEqual(2d, (inch + inch).GetConversionTo("in").Factor);
Assert.AreEqual(0d, (inch - inch).GetConversionTo("1").Factor); 
```

<div id='syntax-sugar-units'/>

### Syntax Sugar
Sometimes it's helpful to have cleaner methods to do basic operations with fewer lines, these are a collection of useful shortcuts. 

```csharp
//"Syntax sugar" to help simply usage
Assert.AreEqual(0, (32, "degF").ConvertTo("degC"), 1E-9);
```

<div id='config-units'/>

### Configuration
Defining units can be tidious to specify each unit for all the many spellings units may take on (one of the reasons
for this implementation). Considering the subsequent snippet of the default json we can see three main configuration
options: prefixes, base units, and units (expressions). 

This configuration can be changed at any time (thread safely) at runtime by using the subsequent snippet. Note that the 
subsequent json deserializes directly into the UnitOfMeasure.Options object as well. By default, the implementation loads the units.json 
present in this repository at static construction. 

```csharp
UnitOfMeasure.Configure(UnitOfMeasure.Options.Default); 
```

```json
{
	"prefixes":	{
		"si": {
			"Y":1e24,  "Z":1e21,  "E":1e18,  "P":1e15,  "T":1e12, "G":1e9,   "M":1e6,   
			"k":1e3,   "h":1e2,   "da":1e1,  "d":1e-1,  "c":1e-2,  "m":1e-3,  "u":1e-6,  
			"n":1e-9,  "p":1e-12, "f":1e-15, "a":1e-18, "z":1e-21, "y":1e-24
		}, 
		"si+": {
			"yotta":1e24, "zetta":1e21,  "exa" :1e18,  "peta":1e15,   "tera":1e12, "giga":1e9,   
			"mega":1e6,    "kilo":1e3,   "hecto":1e2,   "deka":1e1, "deci":1e-1,  "centi":1e-2,  
			"milli":1e-3, "micro":1e-6,  "nano":1e-9,  "pico":1e-12, "femto":1e-15, "atto":1e-18, 
			"zepto":1e-21, "yocto":1e-24
		}
	},
	"baseUnits":  [
		"m",     //meter 
		"s"      //second
	],
	"units": {
		// Lengths
		"[si]m, [si+]meter": "m",
		"ft, foot, feet": "0.3048*m",
		"in, inch, inches": "0.0254*m",
		"mi, mile, miles": "1609.344*m",
		"yd, yard, yards": "0.9144*m",

		// Time
		"[si]s, [si+]second, [si+]seconds": "s",
		"min, minute, minutes": "60*s",
		"h, hr, hour, hours": "60*min", 		
		"d, day, days": "24*h",
		"wk, week, weeks": "7*d",
	}
}
```

#### Prefixes
Prefixes allow a quick method to add all the prefixes for a given unit using the provided factors. This ultimately allows 
the configuration to be significantly more concise compared to specifying each variant of an SI unit. For example, 
the "si" prefix applied to meters "m" will fill in nm, um, mm, and so forth while applying the appropriate scale from 
the original expression. 

#### Base Units 
The concept of a base unit is the foundation of this implementation and is derived from the standard: [International System of Quantities](https://en.wikipedia.org/wiki/International_System_of_Quantities).
In this system of quantities, every phsyical unit resolves into the SI base units. For instance, the pressure 
unit of Pascal resolves into the expression "m ^ -1 * kg * s ^ -2". It is important to note that these base units 
themselves cannot containe algebraic expressions as they always have an offset of zero and a factor of one. For 
units to be commensurable with one another, they must share equivalent base units. This implementation supports fractional 
powers m ^ (1/3) for representing various mathematical modeling units and therefore supports a small cumulative tolerance of 0.01 to determine 
when two units are commensurable (m ^ 0.333) is commensurable with (m ^ 0.334) and therefore are convertible. 

#### Unit Expressions
The final section “units” is comprised of unit names that resolve into unit “expressions”. These expressions 
can build upon either the base units, such as how Pascal does, or previously defined units such as how “day” 
is “24 * h”, and “hour” is “60 * min”. You may have already noticed that each entry can consist of multiple names 
separated by commas such as how “h, hr, hour, hours” resolves each name to the expression 60 * min. This is 
just syntactical sugar to help the user more concisely write their conversions – including their irreplaceable 
misspellings. Of course, each unit name cannot itself contain algebra which is reserved only for the 
expressions. You can think of these unit names as short hands to an expression – which for the sake of many
units like “foot” ("ft, foot, feet": "0.3048*m"), these expressions also double as unit conversions. 


