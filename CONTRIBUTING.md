# Contributing

Thanks for helping improve Tractus Presenter Test for NDI.

## Development setup

- Windows x64
- .NET 10 SDK
- NDI 6 runtime for live sender testing

## Before opening a pull request

1. Keep changes focused and preserve the default silent startup behavior.
2. Run `dotnet build TractusPresenterTestForNDI.csproj -c Release`.
3. Run `dotnet test tests/TractusPresenterTest.Tests.csproj -c Release`.
4. Describe any live NDI, audio, or UI testing performed.
5. Do not commit NDI runtime binaries, generated releases, credentials, or user-supplied images.

Bug reports should include the Windows version, NDI runtime version, source count, and concise reproduction steps. Do not include private network details or sensitive media.
