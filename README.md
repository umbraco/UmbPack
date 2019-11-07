# Umbraco-Packager-CLI
DRAFT: A CLI tool to use in CI/CD to upload Umbraco package .zip to our.umbraco.com package repository

## Building the tool
This will create a Nuget package at `src/nupkg`
```
cd src
dotnet pack
```

## Installing the tool
```
dotnet tool install --global --add-source ./nupkg UmbracoPackage
```

## Using the tool
```
umbracopackager --help
umbracopackager --version
umbracopackager --package=My_Awesome_Package.zip --key=JWTKeyHere
```

### Uninstalling the tool
```
dotnet tool list --global
dotnet tool uninstall --global UmbracoPackage
```

