# Orbit D100H

Nederlandstalige Windows-app voor scrollen, knoptoewijzingen en profielen per
programma. Werkt rechtstreeks via Bluetooth HID, zonder Ulanzi Studio,
driverinstallatie, account of internetverbinding.

**[Download de Windows-app](https://github.com/nsaasman/Orbit-D100H/releases/latest)**

## Downloaden en installeren

1. Open de [nieuwste release](https://github.com/nsaasman/Orbit-D100H/releases/latest)
   en download het bestand `Orbit-D100H-v1.0.0-win-x64.zip`.
2. Pak de volledige ZIP uit naar een vaste map op je eigen computer.
3. Koppel de Ulanzi D100H via Bluetooth in Windows en sluit Ulanzi Studio af.
4. Start **Orbit D100H.exe** in de uitgepakte map.

De download is voor Windows x64 en bevat de benodigde .NET-runtime. Een losse
.NET-installatie is niet nodig. Bewaar alle bestanden en mappen bij elkaar.
Gebruik bijwerken via dezelfde stappen en sluit de vorige versie eerst via
**Afsluiten** in het systeemvak. Instellingen staan buiten de programmamap en
blijven behouden.

**Orbit D100H Probe.exe** is een diagnoseprogramma voor ontwikkelaars. Het is
niet de hoofdapp en sluit na inventariseren af. De normale Windows-download
bevat de hoofdapp.

## Starten

Start **Orbit D100H.exe**. Bouw je zelf vanuit de broncode, dan staat deze in
`artifacts/Orbit-D100H-win-x64`. Bewaar de volledige distributiemap bij elkaar:
deze bevat de benodigde .NET-runtime. Sluit
Ulanzi Studio af en zet de gekoppelde D100H aan. Windows moet scrollen in
inactieve vensters onder de muis toestaan. De app geeft uitleg als die
instelling uitstaat; de app wijzigt deze instelling niet zelf.

Er wordt niets op het apparaat geïnstalleerd. De verbinding gebruikt tijdelijk
vendor mode. Na afsluiten of procesuitval herneemt de D100H zijn standaardbediening.

De eerdere naam was Orbit / D100HControl. Bestaande instellingen en het gekozen
thema blijven behouden. Gebruik je **Starten met Windows**, sla de instellingen
dan na het vervangen eenmaal opnieuw op: de opstartverwijzing gaat dan naar
`Orbit D100H.exe`. Sluit de oude versie via het systeemvak voordat je de nieuwe start.

## Bediening

- Kies per profiel **Scrollen** of **Volume** met de schakelaar bij **Draaiknop doet**.
  Volume verandert het Windows-volume één stap per draaistap; de snelheidsinstelling
  geldt alleen voor scrollen. **Richting omkeren** keert de draairichting voor de
  gekozen functie om.
- Rechtsom scrollt omlaag; linksom omhoog.
- Kies vaste wielstappen of vloeiend scrollen. De standaard is drie wielstappen
  per draaistap. Windows en het doelprogramma bepalen hoeveel regels dat zijn.
- Vloeiend scrollen verdeelt dezelfde totale beweging over maximaal 96 ms na
  de laatste draaistap, zonder uitroleffect. Sommige programma's tellen kleine
  bewegingen eerst op en geven daardoor minder vloeiende weergave.
- Scrollsnelheid, scrollmodus en omgekeerde richting zijn per profiel instelbaar.
- Klik een knop op de D100H-afbeelding en kies geen actie, een sneltoets of
  een mediafunctie. De groene knop op de afbeelding is de geselecteerde knop.
- Kies voor **Ctrl**, **Shift**, **Alt** en **Win** afzonderlijk links of rechts,
  plus een hoofdtoets. Bestaande algemene modifiers blijven ongewijzigd bewaard.
- Of klik **Combinatie vastleggen**, druk de combinatie in en laat alle toetsen
  los. Alleen tijdens die opname vangt een tijdelijke Windows-keyboardhook de
  toetsen af. Een gewone Escape annuleert; Tab kan ook worden opgenomen.
  Annuleren, venster sluiten, een ander venster activeren of twintig seconden
  wachten beëindigt de opname zonder de vorige binding te vervangen.
- Tijdens opname voert de D100H geen acties uit. De Bluetooth-verbinding blijft
  actief. Na afloop geldt de eerdere pauzestand weer. Buiten opname draait geen
  keyboardhook en worden geen gewone toetsen onderschept of gelogd.
- Sneltoetsen op de zeven losse knoppen blijven ingedrukt zolang je de knop
  vasthoudt. Loslaten laat de combinatie in omgekeerde volgorde los. De app
  herhaalt geen aanslagen. Gelijktijdige knoppen kunnen modifiers delen.
- Media-acties geven één aanslag bij indrukken. Draaiknopdruk geeft één aanslag
  bij loslaten; het apparaat meldt die druk pas bij loslaten.
- Bij wisselen van actief venster, pauzeren, verbindingsverlies, opslaan,
  toetsen opnemen, schermvergrendeling en afsluiten laat de app vastgehouden
  synthetische toetsen los. Laat de apparaatknop daarna eerst los en druk
  opnieuw om de combinatie te activeren.
- Voeg een programma toe door het `.exe`-bestand te kiezen. Draaiacties volgen
  het programma onder de muis; knopacties volgen het actieve programma. Elk
  profiel kan een eigen draaifunctie, snelheid, richting en knoptoewijzingen hebben.
- Het algemene profiel geldt wanneer er geen programmaprofiel is.
- **Haptische feedback bij draaien** zet het voelbare stapgevoel aan of uit.
  Deze instelling geldt voor alle profielen en wordt na opslaan toegepast.
  De standaard is aan; sterkte en stapdichtheid zijn niet instelbaar.
  Bij opnieuw verbinden past Orbit D100H de opgeslagen keuze opnieuw toe. Offline
  opslaan kan ook: de keuze wordt toegepast zodra de draaiknop verbonden is.
  **Haptische feedback niet bevestigd** betekent dat het apparaat geen passend
  antwoord gaf. Scrollen en knoppen blijven dan werken. Wijzig de keuze en sla
  opnieuw op, of verbind de draaiknop opnieuw, om het nogmaals te proberen.
- Klik **Instellingen opslaan** om wijzigingen toe te passen. Een venster sluiten
  verbergt de editor en bewaart nog niet opgeslagen bewerkingen in het venster.
- **Donker thema** schakelt direct tussen donker en licht. De keuze wordt apart
  van profielwijzigingen onthouden. Het venster past de indeling aan bij minder
  ruimte en biedt verticaal scrollen wanneer de inhoud niet past.
- **Pauzeren** negeert de D100H-bediening, maar behoudt vendor mode. Andere
  volumetoetsen blijven werken. **Afsluiten** staat in het systeemvakmenu.
- **Starten met Windows** staat standaard uit. Na inschakelen en opslaan start
  de app via de gebruikersinstellingen, zonder zichtbaar venster. Plaats de
  distributiemap voor dagelijks gebruik bij voorkeur op de eigen computer.

Instellingen staan in `%LOCALAPPDATA%\D100HControl\settings.json`. Een
beschadigd of ongeldig bestand wordt gemeld en niet overschreven. Sluit in
dat geval de app, bewaar het bestand en herstel het voordat je opnieuw start.

Er kan één app of diagnoseproces tegelijk actief zijn. Verbindingsverlies
wordt gemeld; de app zoekt daarna automatisch opnieuw verbinding. Bij
doelwisseling, pauzeren en verbindingsverlies vervalt resterende scrollbeweging.

Administratorvensters vallen buiten deze versie. Windows kan invoer naar
verhoogde processen blokkeren. De app verhoogt zichzelf niet automatisch.

### Wel gekoppeld, maar niet verbonden

Een Windows-vermelding **Gekoppeld** bewijst geen actieve Bluetooth-verbinding.
De app kan pas verbinden wanneer Windows de HID-collecties beschikbaar maakt.
Controleer voeding en het actieve hostslot van de D100H.

Blijft de verbinding weg, vernieuw dan uitsluitend de Windows-koppeling van
**Ulanzi Dial**. Verwijder die bij Bluetooth-apparaten, houd de apparaatwisselknop
drie seconden ingedrukt tot het lampje knippert en voeg **Ulanzi Dial** opnieuw
toe. De app blijft automatisch zoeken; de appinstellingen worden hierbij niet
gewijzigd. Op de testcomputer bracht dit de ontbrekende HID-collecties terug.

## Knoppen op de afbeelding

De afbeelding is een eigen schaalbare tekening van het bovenaanzicht. De
zichtbare nummering volgt de door de gebruiker geannoteerde afbeelding.
De interne apparaatcodes in opgeslagen toewijzingen blijven behouden:

| Positie | Knop |
| --- | --- |
| Boven links | 3 |
| Boven midden | 4 |
| Boven rechts | 5 |
| Links boven naast de draaiknop | 2 |
| Links onder | 1 |
| Rechts boven naast de draaiknop | 6 |
| Rechts onder | 7 |
| Draaiknopdruk | 8 |

## Bouwen en controleren

Voer deze opdrachten uit in de projectmap, met een .NET 10 SDK:

```powershell
dotnet build .\OrbitD100H.slnx -c Release
dotnet ".\src\OrbitD100H.Probe\bin\Release\net10.0-windows\Orbit D100H Probe.dll" --self-test
dotnet ".\src\OrbitD100H.Probe\bin\Release\net10.0-windows\Orbit D100H Probe.dll" --output .\artifacts\hardware\collections.json
```

De controles omvatten rapportdecodering, profielselectie, knopacties,
scrollrichting, behoud van totale scrolldelta, doelwisseling, instellingenopslag
en bescherming van beschadigde bestanden. Een vertraagd testtransport controleert
ook dat Bluetooth-schrijftijd niet bovenop het keepalive-interval komt.

## Diagnose en hardwareproef

De regressiesuite bevat 229 automatische controles. Op het ontwikkelsysteem
zijn scrollen, knoppen, haptische feedback en opnieuw verbinden praktisch
bevestigd. Herstel na Windows-slaapstand blijft nog onbeproefd. Controleer
de werking op je eigen apparaat met de onderstaande proef.
Het diagnoseprogramma genereert geen synthetische muis- of toetsenbordinvoer.
Zonder `--listen` inventariseert het uitsluitend passende HID-collecties.

Sluit eerst Orbit D100H via het systeemvak en sluit Ulanzi Studio volledig.
Zet de gekoppelde D100H aan. Start vervolgens:

```powershell
dotnet ".\src\OrbitD100H.Probe\bin\Release\net10.0-windows\Orbit D100H Probe.dll" --listen --seconds 180 --output .\artifacts\hardware\vendor-session.json
```

1. Wacht op `Vendor mode bevestigd door keepalive-antwoord.`
2. Draai linksom en rechtsom. Controleer dat het Windows-volume gelijk blijft.
3. Druk de zeven knoppen en de draaiknop eenmaal in en laat ze los.
4. Controleer dat de volumetoetsen van het toetsenbord nog normaal werken.
5. Laat de proef eindigen of druk `Ctrl+C`. Controleer dat de draaiknop weer
   standaardvolume bedient.
6. Herhaal de proef na uit- en inschakelen van het apparaat. Controleer de
   verbinding en alle gebeurtenissen opnieuw.
7. Controleer apart herstel na Windows-slaapstand en na beëindigen van het
   diagnoseproces. Bewaar de meetresultaten en noteer fysieke waarnemingen.

Een keepalive-antwoord bewijst communicatie, maar bewijst op zichzelf niet
dat alle bediening werkt of dat er geen neveneffecten zijn.

Exitcodes: `0` is een afgeronde opdracht, `1` is een fout en `2` betekent dat
geen bruikbare vendorcollectie is gevonden. Exitcode `0` is geen automatische
goedkeuring van de volledige hardwareproef.

De software logt alleen rapport-ID's, eventcodes en samenvattingen. De overige
rapportbytes worden niet opgeslagen: die kunnen apparaatgeheugen bevatten.

Een passieve controle op ongewenste volumecommando's kan naast de app draaien:

```powershell
dotnet ".\src\OrbitD100H.Probe\bin\Release\net10.0-windows\Orbit D100H Probe.dll" --monitor-consumer --seconds 180 --output .\artifacts\hardware\consumer-save.json
```

Deze meting leest uitsluitend de D100H Consumer Control-collectie. Ze activeert
geen vendor mode en blokkeert geen Windows-invoer. `02 E9 00` betekent volume
omhoog, `02 EA 00` volume omlaag en `02 00 00` het loslaten van een mediatoets.

De app controleert bij verbindingsverlies iedere 250 ms op een bruikbare D100H.
Opslaan behoudt de bestaande HID-sessie. Vóór Windows de Bluetooth-verbinding
beschikbaar maakt en vendor mode actief is, kan de D100H nog zijn standaardinvoer
versturen. Andere volumetoetsen worden niet geblokkeerd.

## Bronnen

- [HamDial-protocol](https://github.com/ftl/hamdial/blob/master/doc/protocol.md)
- [D100H homebrew-onderzoek](https://github.com/brendanwelsh/ulanzi-d100h-homebrew)
- [OpenLogi, productvoorbeeld](https://github.com/AprilNEA/OpenLogi)

Zie `THIRD-PARTY-NOTICES.md` voor de licentie van de gebruikte protocolmapping.

## Licentie

Orbit D100H is beschikbaar onder de [MIT-licentie](LICENSE).
De protocolmapping is gebaseerd op HamDial; de bijbehorende vermelding staat
in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
Dit project is onafhankelijk en niet verbonden aan Ulanzi.

## Publiceren

Dubbelklik op `release.bat` om de solution in Release te bouwen, de automatische
controles uit te voeren en de zelfstandige Windows x64-app te publiceren.
Hiervoor is de .NET 10 SDK nodig. Het script werkt ook vanaf een netwerkpad.
Gebruik `release.bat /nopause` vanuit scripts om de afsluitende pauze over te slaan.
Bij een fout stopt het script met een niet-nul exitcode. Sluit een draaiende
release via **Afsluiten** in het systeemvak als bestanden vergrendeld zijn.

De uitvoer staat in `artifacts\Orbit-D100H-win-x64`. Distribueer die volledige
map. De ontvanger heeft geen afzonderlijke .NET-runtime nodig.

De publicatiestap kan ook rechtstreeks worden uitgevoerd:

```powershell
dotnet publish .\src\OrbitD100H.App\OrbitD100H.App.csproj -c Release -r win-x64 --self-contained true -o .\artifacts\Orbit-D100H-win-x64
```

De app gebruikt WPF, een native systeemvakmenu en Windows HID- en invoer-API's.
Er zijn geen externe NuGet-afhankelijkheden. De distributie is voor Windows x64.

Voor het afzonderlijke diagnoseprogramma:

```powershell
dotnet publish .\src\OrbitD100H.Probe\OrbitD100H.Probe.csproj -c Release -r win-x64 --self-contained true -o .\artifacts\Orbit-D100H-Probe-win-x64
& ".\artifacts\Orbit-D100H-Probe-win-x64\Orbit D100H Probe.exe" --self-test
```

De volledige map bevat de benodigde runtime en de licentievermelding. Een
ontvanger hoeft geen .NET-runtime te installeren. Gebruik `--listen` expliciet
om vendor mode te activeren; zonder die optie wordt alleen geïnventariseerd.
