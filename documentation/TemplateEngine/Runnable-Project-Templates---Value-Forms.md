## Overview

At a high level, value forms allow the specification of a "replaces"/"replacement" pair to also apply to other ways the "replaces" value may have been specified in the source by specifying a transform from the original value of "replaces" in configuration to the one that may be found in the source.

Ex.
In my source, the value `"Hi there"` should be replaced by a user supplied value, but, `&quot;Hi there&quot;` should be as well - the user value should be XML encoded for the latter and not for the former. The below snippet from template.json shows how to accomplish this.

```
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

replace   - Perform a replacement with regular expressions
```
            `identifier`  -> `replace`
            `pattern`     -> A regular expression matching the characters to replace
            `replacement` -> The replacement for the matched characters
```

chain     - Performs multiple transformations
```
            `identifier`  -> `chain`
            `steps`       -> An array of names of other transformations (applied in the order they appear in the array)
```

xmlEncode - XML encodes a value
```
            `identifier`  -> `xmlEncode`
```

lowerCase - Lower cases a value
```
            `identifier`  -> `lowerCase` or `lowerCaseInvariant`
```

upperCase - Upper cases a value
```
            `identifier`  -> `upperCase` or `upperCaseInvariant`
```

`firstLowerCase` - converts the first letter of the value to lowercase using the casing rules of the current culture. Available since version 5.0.200.
```
    "forms": {
        "first_lc": {
          "identifier": "firstLowerCase"
        }
    }
```
`firstLowerCaseInvariant` - converts the first letter of the value to lowercase using the casing rules of the invariant culture. Available since version 5.0.200.
```
    "forms": {
        "first_lc": {
          "identifier": "firstLowerCaseInvariant"
        }
    }
```

`firstUpperCase` - converts the first letter of the value to uppercase using the casing rules of the current culture. Available since version 5.0.200.
```
    "forms": {
        "first_uc": {
          "identifier": "firstUpperCase"
        }
    }
```
`firstUpperCaseInvariant` - converts the first letter of the value to uppercase using the casing rules of the invariant culture. Available since version 5.0.200.
```
    "forms": {
        "first_uc": {
          "identifier": "firstUpperCaseInvariant"
        }
    }
```

`titleCase` - converts the value to title case using the casing rules of the current culture. See [TextInfo.ToTitleCase(String) documentation](https://docs.microsoft.com/dotnet/api/system.globalization.textinfo.totitlecase) for more details. Available since version 5.0.200.
```
    "forms": {
        "title": {
          "identifier": "titleCase"
        }
    }
```

`kebabCase` - converts the value to kebab case using the casing rules of the invariant culture. Available since version 5.0.200.
```
    "forms": {
        "kebab": {
          "identifier": "kebabCase"
        }
    }
```