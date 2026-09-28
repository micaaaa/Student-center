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

## Dokumenti uz prijavu

Pokrenuti ApplicationService ponovo da primeni migraciju AddApplicationDocuments. Fajlovi se cuvaju privatno u ApplicationService/App_Data/Documents, van Git-a i javnog web direktorijuma. Pri preseljenju aplikacije potrebno je sacuvati i bazu i ovaj direktorijum.

Za studenta sa profilom i prijavom DRAFT:

1. POST /api/applications/{applicationId}/documents: multipart/form-data; File je PDF, JPG/JPEG ili PNG do 10 MB. DocumentType: 1 potvrda o upisu, 2 uverenje o prihodima, 3 prepis ocena, 4 identifikacioni dokument, 5 ostalo. Swagger prikazuje izbor fajla. Uspeh vraca 201 i status PENDING.
2. GET /api/applications/{applicationId}/documents: lista metapodataka, bez interne putanje fajla.
3. GET /api/applications/{applicationId}/documents/{documentId}/download: privatno preuzimanje fajla sa originalnim nazivom.
4. DELETE /api/applications/{applicationId}/documents/{documentId}: brisanje dokumenta iz nacrta, odgovor 204.
5. Posle slanja prijave pregled i preuzimanje i dalje rade; dodavanje i brisanje vracaju 409.
6. Drugi student dobija 404. Nepostojeci dokument ili dokument iz druge prijave takodje vraca 404.
7. Nepodrzan format, prazan fajl ili nepodudaranje ekstenzije i potpisa vracaju 400; prevelik fajl 413 (infrastrukturni limit multipart zahteva takodje moze odbiti zahtev pre kontrolera).

Provera formata koristi pocetni potpis fajla; nije potpuna provera ispravnosti sadrzaja niti antivirusno skeniranje. Za ovaj korak dokumenti nisu obavezan uslov za slanje prijave; pravila obavezne dokumentacije i pregled osoblja dolaze kasnije.

Testovi dokumenata pokrivaju autorizaciju na nivou servisa, dozvoljene statuse, velicinu, format, preuzimanje istih bajtova i kompenzaciju neuspelog upisa u bazu. Ako fizicko brisanje ne uspe nakon uklanjanja zapisa, fajl ostaje nedostupan kroz API, a greska se upisuje u log radi naknadnog ciscenja.

## Obrada prijava i dokumentacije

Restartovati ApplicationService radi migracije AddDocumentReview. Za /api/staff/applications potreban je vazeci JWT sa ulogom STAFF ili ADMIN i dozvolom ManageApplications. Studentski token nema pristup ni kada ima tu dozvolu.

1. GET /api/staff/applications?competitionId={id}&page=1 vraca do 50 ranije poslatih prijava, od najstarije. Opcioni status koristi enum vrednosti Submitted, UnderReview, Accepted, Rejected, Withdrawn. Nacrti i prijave povucene pre slanja nisu vidljivi.
2. GET /api/staff/applications/{applicationId} vraca detalje.
3. POST /api/staff/applications/{applicationId}/start-review menja SUBMITTED u UNDER_REVIEW. Ponovni poziv ili nedozvoljen status vraca 409. Student vise ne moze da povuce prijavu.
4. GET /api/staff/applications/{applicationId}/documents vraca dokumente. GET /api/staff/applications/{applicationId}/documents/{documentId}/download preuzima fajl.
5. PUT /api/staff/applications/{applicationId}/documents/{documentId}/review sa telom {"status":2,"comment":"Dokument je citljiv i potpun."} oznacava VALID. Za INVALID koristiti status 3 i obavezno obrazlozenje. Status PENDING (1) nije dozvoljen za rezultat provere.
6. Odgovor sadrzi ReviewComment, ReviewedAtUtc i ReviewedByUserId. Identitet sluzbenika se uzima iz tokena, ne iz tela zahteva. Komentar ima najvise 2000 karaktera.
7. Student preko svog postojeceg endpoint-a za listu dokumenata vidi status, komentar i podatke o proveri. Osoblje moze korigovati rezultat dok je prijava UNDER_REVIEW; cuva se poslednji rezultat, ne istorija svih izmena.

Neispravan zahtev vraca 400, nepostojeca prijava/dokument ili neslata prijava 404, nedozvoljena promena statusa 409. Konkurentna izmena prijave ili rezultata provere takodje vraca 409 i zahteva ponovno ucitavanje. SQL Server konkurentnost i kompletan HTTP tok treba dodatno proveriti sa pokrenutim servisima; unit testovi koriste memorijske repozitorijume. Bodovanje i konacna odluka jos nisu deo ove celine.
