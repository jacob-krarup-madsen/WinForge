---
description: Build WinForge in Release and report warnings
agent: dotnet-quality
---

Build the solution in Release, treating warnings as errors:

1. Run `dotnet restore WinForge.sln`
2. Run `dotnet build WinForge.sln -c Release`
3. Report warning IDs / errors by file, or confirm clean build.

Args: $ARGUMENTS (extra msbuild args, e.g. `-p:Platform=x64`)
