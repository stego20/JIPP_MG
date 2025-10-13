dotnet build
dotnet test tests/Api.Tests/Api.Tests.csproj
dotnet watch run --project src/Api/Api.csproj

http://localhost:{port}/hello/{imie}
http://localhost:{port}/api/v1/health
