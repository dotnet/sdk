# Value forms

## Overview

At a high level, value forms allow the specification of a "replaces"/"replacement" pair to also apply to other ways the "replaces" value may have been specified in the source by specifying a transform from the original value of "replaces" in configuration to the one that may be found in the source.


*Example*

In my source, the value `"Hi there"` should be replaced by a user supplied value, but, `&quot;Hi there&quot;` should be as well. The user value should be XML encoded for the latter and not for the former. The below snippet from template.json shows how to accomplish this.

```json
{
  ...
  "symbols": {
    "example": {
      "type": "parameter",
      "dataType": "string",
      "replaces": "\"Hi there\"",
      "forms": {
        "global": [ "encode" ]
      }
    }
  },
  "forms": {
    "encode": {
      "identifier": "xmlEncode"
    }
  }
}
```

The `forms` section (peer to `symbols`) defines the set of transforms that can be referenced by symbol definitions. In the symbol called `example`, the entry from the `forms` section is referenced for all file patterns (the property `global` indicates that the transform should be included everywhere).

When the value say, `Test ©` is supplied as the value for the parameter `example`, two replacements get set up:
1) `"Hi There"` -> `Test ©`
2) `&quot;Hi There&quot;` -> `Test &copy;`

## Available transforms:

**`replace`**   - Performs a replacement with regular expressions
```json
"forms": {
    "dotToUnderscore": {
      "identifier": "replace",
      "pattern": "\\.",        // A regular expression matching the characters to replace
      "replacement": "_"       // The replacement for the matched characters
    }
}
```

**`chain`**     - Performs multiple transformations
```json
"forms": {
    "chained": {
      "identifier": "chain",
      "steps": [ "dotToUnderscore", "digitToBang" ]  // An array of names of other transformations (applied in the order they appear in the array)
    },
}
```

**`xmlEncode`** - XML encodes a value
```json
"forms": {
    "encode": {
      "identifier": "xmlEncode"
    }
}
```

**`lowerCase`** - Converts the letters of the value to lowercase using the casing rules of the current culture.
```json
"forms": {
    "lc": {
      "identifier": "lowerCase"
    }
}
```

**`lowerCaseInvariant`** - Converts the letters of the value to lowercase using the casing rules of the invariant culture.
```json
"forms": {
    "lc": {
      "identifier": "lowerCaseInvariant"
    }
}
```

**`upperCase`** - Converts the letters of the value to uppercase using the casing rules of the current culture.
```json
"forms": {
    "uc": {
      "identifier": "upperCase"
    }
}
```

**`upperCaseInvariant`** - Converts the letters of the value to uppercase using the casing rules of the invariant culture.
```json
"forms": {
    "uc": {
      "identifier": "upperCaseInvariant"
    }
}
```

**`firstLowerCase`** - Converts the first letter of the value to lowercase using the casing rules of the current culture. Available since .NET 5.0.300.
```json
"forms": {
    "first_lc": {
      "identifier": "firstLowerCase"
    }
}
```

**`firstLowerCaseInvariant`** - Converts the first letter of the value to lowercase using the casing rules of the invariant culture. Available since .NET 5.0.300.
```json
"forms": {
    "first_lc": {
      "identifier": "firstLowerCaseInvariant"
    }
}
```

**`firstUpperCase`** - Converts the first letter of the value to uppercase using the casing rules of the current culture. Available since .NET 5.0.300.
```json
"forms": {
    "first_uc": {
      "identifier": "firstUpperCase"
    }
}
```

**`firstUpperCaseInvariant`** - Converts the first letter of the value to uppercase using the casing rules of the invariant culture. Available since .NET 5.0.300.
```json
    "forms": {
        "first_uc": {
          "identifier": "firstUpperCaseInvariant"
        }
    }
```

**`titleCase`** - Converts the value to title case using the casing rules of the current culture. See [TextInfo.ToTitleCase(String) documentation](https://docs.microsoft.com/dotnet/api/system.globalization.textinfo.totitlecase) for more details. Available since .NET 5.0.300.
```json
"forms": {
    "title": {
      "identifier": "titleCase"
    }
}
```

**`kebabCase`** - Converts the value to kebab case using the casing rules of the invariant culture. Available since .NET 5.0.300.
```json
"forms": {
    "kebab": {
      "identifier": "kebabCase"
    }
}
```