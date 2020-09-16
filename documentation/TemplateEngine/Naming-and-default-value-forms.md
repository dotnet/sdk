When selecting a source name for a template you're authoring, keep in mind the default value forms applied to the `name` symbol. A good choice for name is something that produces distinct values under the below transformations (like `Template.1` does). The first two tables below give examples of `sourceName` to target name mappings (target names being strings that can be found in the template content). The `name` value that the user supplies goes through the same transformation as `sourceName` does and the outputs for each transformation are mapped to eachother, the third table below gives an example of this process (note that while the string that will be injected isn't necessarily unique, the strings being replaced are, allowing the template author to select what the appropriate transform on the user input is)

***Unambiguous***

Transform | Input | Output
-------|------|--------
Identity | Template.1 | Template.1
Namespace | Template.1 | Template._1
Class Name | Template.1 | Template__1
Lowercase Namespace | Template.1 | template._1
Lowercase Class Name | Template.1 | template__1

***Ambiguous***

Transform | Input | Output
-------|------|--------
Identity | template1 | template1
Namespace | template1 | template1
Class Name | template1 | template1
Lowercase Namespace | template1 | template1
Lowercase Class Name | template1 | template1


***Full mapping***

Transform | sourceName | name | generated replacement
-------|------|--------|--------
Identity | Template.1 | My-App | Template.1 -> My-App
Namespace | Template.1 | My-App | Template._1 -> My_App
Class Name | Template.1 | My-App | Template__1 -> My_App 
Lowercase Namespace | Template.1 | My-App | template._1 -> my_app
Lowercase Class Name | Template.1 | My-App | template__1 -> my_app
