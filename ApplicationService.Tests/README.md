# Provera toka studentske prijave

Automatski testovi (bez baze i pokrenutih servisa):

```powershell
dotnet test ApplicationService.Tests/ApplicationService.Tests.csproj
```

## Rucna provera kroz Swagger

1. Pokrenuti IdentityService, StudentService i ApplicationService. ApplicationService pri pokretanju primenjuje migracije.
2. Korisnikom sa dozvolom ManageApplications kreirati konkurs sa pocetkom u proslosti i krajem u buducnosti, pa ga otvoriti.
3. Prijaviti studenta, kreirati njegov profil i postaviti studentski token u ApplicationService Swagger-u.
4. POST /api/applications sa competitionId i opcionim note vraca DRAFT. Sacuvati id.
5. GET /api/applications/me i GET /api/applications/{id} prikazuju prijavu.
6. PUT /api/applications/{id} sa telom {"note":"Izmenjena napomena"} menja napomenu. null ili prazna vrednost je uklanjaju.
7. POST /api/applications/{id}/submit vraca SUBMITTED i vreme slanja.
8. Ponovno slanje ili izmena poslate prijave vracaju 409.
9. POST /api/applications/{id}/withdraw vraca WITHDRAWN i cuva vreme ranijeg slanja. Ponovno povlacenje vraca 409.
10. Drugi student sa kreiranim profilom za isti id dobija 404 pri pregledu, izmeni, slanju i povlacenju.
11. Slanje nacrta na zatvoren konkurs, pre pocetka ili posle isteka roka vraca 409. Isto vazi za kreiranje prijave.

Povlacenje je dozvoljeno za DRAFT i SUBMITTED i nakon isteka konkursa, dok obrada jos nije pocela. UNDER_REVIEW, ACCEPTED, REJECTED i WITHDRAWN ne mogu se povuci. Povucena prijava ostaje evidentirana; ponovna prijava na isti konkurs nije podrzana.

Nepostojeca ili tudja prijava vraca 404, sukob poslovnih pravila i nedostajuci studentski profil 409, a nedostupan StudentService 503. Neispravno JSON telo proverava ASP.NET Core (400). Autentifikaciju i ulogu STUDENT proverava autorizacija (401/403).

Testovi koriste memorijske zamene repozitorijuma i Student klijenta. Ne proveravaju SQL Server, JWT middleware niti komunikaciju izmedju pokrenutih servisa.
