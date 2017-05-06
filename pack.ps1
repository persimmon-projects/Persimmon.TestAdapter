dotnet restore
dotnet build -c Release
.\.nuget\NuGet.exe pack .\Persimmon.TestAdapter.nuspec