# NEXUS Network Center Update

## Changes
- Added a user-triggered DNS resolution check for `example.com`, with elapsed time and clear success/failure state.
- Added a route/address summary showing Windows-reported default gateway, active unicast addresses, and DNS servers.
- Replaced a mojibake dash in adapter address fallbacks.
- Kept all network checks explicit and user-triggered. No packet capture, port scanning, remote-device discovery, DNS changes, firewall changes, or route changes are performed.

## Validation status
- Source patch applied and inspected.
- ZIP integrity will be checked after packaging.
- .NET build, automated tests, and Windows runtime behavior have NOT been verified in this environment. Run restore/build/test on Windows before treating this as release-ready.

## Windows validation
```powershell
dotnet restore .\NEXUS.sln
dotnet build .\NEXUS.sln -c Release
dotnet test .\NEXUS.sln -c Release
```
