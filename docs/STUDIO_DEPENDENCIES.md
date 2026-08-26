# DarkGrey RPG Studio dependencies

The Phase 1 desktop application uses only the .NET 10 SDK, WPF, and the existing Studio Core project. No third-party UI, MVVM, serialization, logging, or packaging dependency has been added.

Build and test use the repository-local NuGet cache under `.tooling/wpf-build/nuget-packages` with `E:\Java\dotnet-sdk-10\dotnet.exe`.

