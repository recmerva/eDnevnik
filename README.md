# Moj dnevnik

Elektronski školski dnevnik za administratore, nastavnike i roditelje: React + TypeScript (Vite), ASP.NET Core 9 API i MySQL 8.4.

## Funkcionalnosti

| Oblast | Šta sistem radi |
| --- | --- |
| Nalozi i prava | Prijava/odjava (kolačić, CSRF zaštita, ograničen broj pokušaja). Uloge: administrator, nastavnik, roditelj. Svako pravo proverava API na svakom zahtevu. |
| Ocene | Unos, izmena i brisanje ocena po predmetu i vrsti; automatski prosek po predmetu i ukupno; predlog zaključne ocene; evidencija zaključnih ocena po polugodištima; opšti uspeh. Veronauka se ocenjuje opisno i ne ulazi u prosek. |
| Izostanci | Evidencija po časovima, opravdani/neopravdani, ukupni pregled; svaki nastavnik upisuje izostanak, a opravdava ga razredni starešina; roditelj može podneti žalbu, razredni odgovara. |
| Raspored | Raspored časova po danima, predmeti i nastavnici odeljenja; administrator na istoj strani menja časove i bira nastavnika za predmet, a izmene rasporeda automatski šalju obaveštenje. |
| Obaveze | Domaći zadaci sa rokom i uputstvom, kontrolni i pismeni zadaci, obaveštenja za odeljenje ili celu školu. |
| Izveštaji | Uspeh učenika (prosek, rang, opšti uspeh, izostanci), uspeh odeljenja (raspodela uspeha, tabela učenika), izvoz u PDF preko štampe. |
| Početna strana | Nastavnik posle prijave vidi svoje današnje časove (koji je u toku, koji sledi) i bira odeljenje; administrator vidi šta se trenutno predaje u svakom odeljenju i koje dodele nedostaju. |
| Komunikacija | Razgovori roditelja sa razrednim starešinom i svakim nastavnikom odeljenja (posebno za svako dete); nastavnik piše roditeljima učenika iz svojih odeljenja; zvonce sa obaveštenjima o novim ocenama, izostancima, porukama, rokovima domaćih, testovima i izmenama rasporeda. |
| Administracija | Nalozi (kreiranje, izmena, nova lozinka, brisanje), učenici i kodovi za registraciju roditelja, nastavnici odeljenja (za svaki predmet jedan nastavnik i razredni starešina; učitelj u I–IV razredu jednim izborom dobija sve svoje predmete, a izbor ostaje dok ga administrator ne promeni), odeljenja i predmeti, školska godina i polugodišta, dnevnik aktivnosti. |

### Ko šta sme

- **Administrator** vodi nalog škole: naloge, učenike, odeljenja, predmete, dodele, raspored i školsku godinu. Vidi ocene, izostanke i obaveze, ali ih ne unosi i ne menja; to rade nastavnici. Na stranici Odeljenje vidi kod svakog učenika (npr. `K7MR-Q4TB`) koji roditelju saopštava lično u školi.
- **Predmetni nastavnik** prvo bira odeljenje u kome predaje, pa učenika. Vidi i unosi ocene, zaključne ocene, testove i domaće samo iz svog predmeta; izostanak može samo da upiše.
- **Razredni starešina** vidi sve ocene svog odeljenja i opšti uspeh, opravdava, menja i briše izostanke i odgovara na žalbe; ocenjuje samo predmet koji predaje.
- **Učitelj** (I–IV razred) je razredni starešina koji predaje sve predmete osim engleskog jezika i veronauke, pa ih i zaključuje.
- **Roditelj** se registruje kodom učenika koji dobije od škole i vidi samo podatke svoje dece. Svako dete ima svoj kod; drugo dete se povezuje u meniju sa imenom deteta („Poveži još jedno dete”) ili ponovnom registracijom istim emailom i lozinkom, pa sva deca ostaju na jednom nalogu.

## Struktura projekta

```
backend/
  Controllers/   API rute (auth, ocene, izostanci, raspored, obaveze, poruke, izveštaji, administracija...)
  Contracts/     modeli zahteva sa validacijom i porukama na srpskom
  Data/          SchoolDbContext i početni podaci (seed)
  Models/        entiteti baze
  Services/      prava pristupa, dnevnik aktivnosti, CSRF filter, kodovi za registraciju
frontend/src/
  api/           klijent za API (CSRF token, greške, odjava pri isteku sesije)
  components/    zajednički delovi interfejsa (meni, zvonce, modal, izbor deteta)
  lib/           formatiranje datuma, računanje proseka i uspeha, učitavanje podataka
  pages/         stranice; pages/admin/ sadrži sekcije administracije
  styles/        CSS
```

## Lokalno pokretanje

1. Pokreni MySQL 8.4: `docker compose up -d`.
2. U PowerShell-u postavi prvi administratorski nalog i pokreni API:
   `$env:SEED_ADMIN_EMAIL="admin@skola.local"; $env:SEED_ADMIN_PASSWORD="IzaberiSvojuJakuLozinku123!"; dotnet run --project backend --urls http://localhost:5080`
3. U drugom terminalu: `cd frontend`, zatim `npm install` i `npm run dev`.
4. Otvori `http://localhost:5173` i prijavi se kao administrator. U **Administracija → Nalozi** napravi nastavnike, u **Nastavnici odeljenja** za svako odeljenje izaberi razrednog i nastavnika za svaki predmet, a u **Učenici i kodovi** preuzmi kod za registraciju roditelja.

API health ruta je `http://localhost:5080/api/health`. Lokalna veza ka bazi je u `backend/appsettings.json`; za drugo okruženje koriste se `MYSQL_HOST`, `MYSQL_PORT`, `MYSQL_DATABASE`, `MYSQL_USER` i `MYSQL_PASSWORD`. Ime škole se menja u `backend/appsettings.json` (`School`).

Baza se kreira pri prvom startu API-ja (`EnsureCreated`) i samo tada se popunjava sa deset odeljenja (I–VIII), 18 predmeta po nastavnom planu, rasporedom časova, ocenama, testovima i izostancima. Ako se postavi i `SEED_DEMO_PASSWORD`, kreiraju se i primer-nalozi nastavnika (`ime.prezime@skola.local`) sa dodelama i dva primer-roditelja (`@primer.rs`), svi sa tom lozinkom. Primer-škola ima 27 nastavnika; predmeti sa više časova (srpski, matematika, nemački, biologija, fizičko) imaju po dva nastavnika koji dele starija odeljenja. Namerno nisu popunjene sve dodele: I-2 nema učitelja (Amela Šabotić još nije raspoređena), VII-2 nema razrednog, Informatika u VII-2 i VIII-1 nema nastavnika, a Muzička kultura u starijim razredima čeka nastavnicu Selmu Kalač, da bi se dodeljivanje moglo isprobati kroz administraciju. Kasnije izmene (dodele, raspored, nalozi) ostaju sačuvane i posle restarta. Ako postoji baza iz starije verzije projekta, obriši je jednom (`docker compose down -v`) da bi se napravile nove tabele. Za buduće izmene šeme preporučuju se EF Core migracije.

## Render deploy

1. Napravi GitHub repozitorijum i push-uj projekat.
2. U Render Dashboard-u izaberi **New > Blueprint**, poveži repozitorijum i potvrdi `render.yaml`.
3. Pri prvom kreiranju unesi jake, različite vrednosti za `MYSQL_PASSWORD`, `MYSQL_ROOT_PASSWORD`, `SEED_ADMIN_EMAIL` i `SEED_ADMIN_PASSWORD`. Tajne se unose direktno u Render, nikad u Git.
4. MySQL je privatni Docker servis sa trajnim diskom na `/var/lib/mysql` (zahteva plaćeni Render plan).
5. Kada `moj-dnevnik-api` postane healthy, prijavi se kao administrator i nastavi kao u koraku 4 lokalnog pokretanja.

Za backup baze koristi `mysqldump`.

**Podrška runtime-a:** projekat cilja .NET 9, čija je podrška završena 10. novembra 2026. Pre dugoročnog produkcionog rada ažuriraj target framework, Docker image i Pomelo EF Core provider na podržanu verziju (.NET 10).
