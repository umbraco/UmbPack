# UmbPack

UmbPack is a CLI tool to use in CI/CD to upload Umbraco .zip packages to the [our.umbraco.com package repository](https://our.umbraco.com/packages/).

If you are looking for info on how to use the tool, check out [the documentation](https://our.umbraco.com/documentation/Extending/Packages/UmbPack) for it instead!

## Building the tool

This will create a Nuget package at `src/nupkg`

```
cd src
dotnet pack -c Release
```

## Installing the tool

```
dotnet tool install --global --add-source ./nupkg UmbPack
```

### Uninstalling the tool

```
dotnet tool list --global
dotnet tool uninstall --global UmbPack
```
