# .NET Compiler Developer SDK

The .NET Compiler Developer SDK is an extension to the standard C# experience in VSCode, providing tools for working with the Roslyn compiler.

## Features

### Syntax Visualizer

Visualize the syntax in a C# file live, as you type, and navigate around the structure interactively.

![Syntax visualizer demonstration](./images/SyntaxVisualizerDemo.gif)

## Requirements

This extension depends on the [C# extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp).

## Extension Settings

`"compilerdevelopersdk.enableSyntaxVisualizer"` - Turn on or off the C# Syntax Visualizer (defaults to on).

## Known Issues

Currently none.

## Release Notes

### 0.1.1

* 4c2e3f3 Correct conditional display of the syntax visualizer, and contribute the setting for disabling it.
* d60da59 Only request the node at the current location if the file type is C#.
* 1e5f735 Add extension startup logging.

### 0.1.0
- First prerelease
