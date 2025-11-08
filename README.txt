komendy w termianlu
dotnet build
dotnet test tests/Api.Tests/Api.Tests.csproj
dotnet watch run --project src/Api/Api.csproj

swagger
http://localhost:5142/swagger


 w miejsca {} wstawic swoje wartosci
Tydzień 1
http://localhost:{port}/hello/{imie}
http://localhost:{port}/api/v1/health

Tydzień 2
dodanie nowego uzytkownika do bazy danych 
Invoke-WebRequest -Uri "http://localhost:{port}/users" -Method POST -ContentType "application/json" -Body '{"username":"{imie}","email":"{mail}"}'
http://localhost:{port}/users -lista wszystkich uzytkownikow

Tydzień 3
http://localhost:{port}/users/{id_usera} - pojedynczy uzytkownik

aktualizacja danych uzytkownika do bazy danych 
Invoke-WebRequest -Uri "http://localhost:{port}/users/{id_usera}" -Method PUT -ContentType "application/json" -Body '{"username":"{imie}","email":"{mail}"}'

usuwanie uzytkownika z bazy danych
Invoke-WebRequest -Uri "http://localhost:{port}/users/{id_usera}" -Method DELETE -ContentType "application/json"

Tydzień 4
dodanie tasku do usera
Invoke-WebRequest -Uri "http://localhost:{port}/tasks" -Method POST -ContentType "application/json" -Body '{"Title":"test","Description":"test przeszedł","UserID":3}'
