komendy w termianlu
dotnet build
dotnet test tests/Api.Tests/Api.Tests.csproj
dotnet watch run --project src/Api/Api.csproj


linki do mapgetów
http://localhost:{port}/hello/{imie}
http://localhost:{port}/api/v1/health
http://localhost:5142/api/v1/user-list -lista wszystkich uzytkownikow
http://localhost:5142/api/v1/user/{id_usera} - pojedynczy uzytkownik



dodanie nowego uzytkownika do bazy danych 
Invoke-WebRequest -Uri "http://localhost:{port}/api/v1/user" -Method POST -ContentType "application/json" -Body '{"username":"{imie}","email":"{mail}"}'
