# Local patch notes

Applied changes:

- Texture transparency: removed DDS texture siblings and normalized near-transparent PNG pixels so RimWorld loads PNG alpha instead of broken DDS alpha.
- Work type: verified the seed-processing work giver is assigned to `Cooking` in 1.4, 1.5, and 1.6 XML.
- Growing zone UI source change: added a `See all plants` toggle gizmo for growing zones and plant growers. When enabled, the plant selector no longer hides plants merely because no seeds are currently in stock; sowing still requires seeds.
- Seed extraction bill source change: added an `Add seed extraction bill` gizmo to the seed processing bench/spot. It opens a menu of seed-extractable produce currently in stock, marks entries whose matching bill already exists, creates correctly filtered missing bills, sets repeat mode to `Do until X`, sets the target count and ingredient search radius from mod settings, and labels the bill after the output seed.
- Seed extraction bulk bill source change: added an `Add missing seed extraction bills` gizmo and a matching menu option to create all missing seed extraction bills for produce currently in stock.
- Mod options source change: added defaults for quick seed-extraction bill target count and ingredient search radius.
- Fixed one malformed French translation XML tag.

Build note:

The C# source is patched, but the bundled DLL must be rebuilt for the UI/bill helper changes to take effect. Use `Source/build-linux.sh` with a .NET SDK, or run:

```bash
cd Source
dotnet restore SeedsPleaseLiteRedux.csproj
dotnet build SeedsPleaseLiteRedux.csproj --configuration Release --no-restore
```

The build writes `1.6/Assemblies/SeedsPleaseLiteRedux.dll`.
