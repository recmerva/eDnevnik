# Moj dnevnik – Elektronski školski dnevnik

**Moj dnevnik** je web aplikacija namenjena digitalizaciji školskog dnevnika i unapređenju komunikacije između škole, nastavnika i roditelja. 
Cilj projekta je da omogući jednostavno, pregledno i efikasno vođenje školske evidencije, uz jasno definisana prava pristupa za svakog korisnika.

Sistem razvijam kroz tri glavne korisničke uloge: **administrator, nastavnik i roditelj**, pri čemu svaka uloga ima različite mogućnosti i pristup odgovarajućim podacima.

### Glavne funkcionalnosti

- **Administracija:** Upravljanje korisničkim nalozima, učenicima, odeljenjima, predmetima, nastavnicima, rasporedom časova i školskom godinom.
- **Ocene:** Unos, izmena i pregled ocena, automatsko računanje proseka, zaključivanje ocena i praćenje opšteg uspeha učenika.
- **Izostanci:** Evidencija prisustva na časovima, opravdavanje izostanaka i komunikacija između roditelja i razrednog starešine.
- **Raspored časova:** Pregled i izmena rasporeda za odeljenja, uz automatsko obaveštavanje korisnika o promenama.
- **Domaći zadaci i testovi:** Objavljivanje domaćih zadataka, kontrolnih i pismenih zadataka sa rokovima i dodatnim uputstvima.
- **Izveštaji:** Pregled uspeha pojedinačnih učenika i celih odeljenja, statistika ocena, izostanaka i opšteg uspeha.
- **Komunikacija:** Direktna komunikacija između roditelja i nastavnika, uz sistem obaveštenja o važnim promenama i aktivnostima.
- **Korisnički profili:** Roditelji mogu povezati više dece sa jednim nalogom i pratiti školske obaveze svakog deteta zasebno.

### Tehnologije

Na projektu koristim sledeće tehnologije:

- **Frontend:** React, TypeScript, Vite
- **Backend:** ASP.NET Core 9 Web API (C#)
- **Baza podataka:** MySQL 8.4
- **ORM:** Entity Framework Core
- **Deployment:** Render
- **Verzionisanje koda:** Git i GitHub

### Bezbednost i kontrola pristupa

Posebnu pažnju posvećujem bezbednosti podataka i zaštiti privatnosti učenika. Sistem uključuje autentifikaciju korisnika, kontrolu pristupa
na osnovu korisničkih uloga, CSRF zaštitu, ograničavanje broja pokušaja prijave i evidenciju aktivnosti.
Svaki korisnik može pristupiti isključivo podacima i funkcionalnostima koje su mu dozvoljene u okviru njegove uloge.

### Cilj projekta

Kroz ovaj projekat želim da razvijem funkcionalan i praktičan informacioni sistem koji bi mogao da se koristi u stvarnom školskom okruženju. 
Pored implementacije osnovnih funkcionalnosti elektronskog dnevnika, cilj mi je da unapredim svoje znanje iz razvoja full-stack web aplikacija, 
projektovanja baza podataka, razvoja API-ja i implementacije bezbednih sistema.
