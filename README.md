# Signature Manager 3

Konsolowa aplikacja VB.NET dla .NET Framework 4.8, przeznaczona do raportowania podpisów cyfrowych w dokumentach PDF.

## Wymagania

- Windows 11 i .NET Framework 4.8,
- Visual Studio 2019 z obsługą VB.NET,
- dostęp do źródła pakietów NuGet,
- plik Excel z arkuszem `SAP_AK` i nagłówkami `Numer osobowy`, `E-mail`, `Pesel`, `Pion`, `Biuro`.

Pakiety NuGet zostaną odtworzone podczas kompilacji. iText Community jest udostępniany na licencji AGPL; przed wdrożeniem w zamkniętym środowisku firmowym należy uzyskać odpowiednią licencję komercyjną albo zatwierdzić zgodność sposobu dystrybucji z AGPL.

## Instalacja zależności

Zależności są zapisane jako `PackageReference` w pliku
`SignatureManager/SignatureManager.vbproj`. Nie należy pobierać bibliotek DLL ręcznie ani
dodawać ich przez **Add Reference**. Zalecaną metodą jest odtworzenie pakietów przez
NuGet bezpośrednio w Visual Studio.

### Projekt SDK a klasyczny projekt Visual Basic

Plik projektu w tym repozytorium używa skróconego formatu SDK (`<Project
Sdk="Microsoft.NET.Sdk">`) i deklaruje zależności jako `PackageReference`. Projekt
utworzony w Visual Studio jako klasyczna aplikacja **Console App (.NET Framework)**
może natomiast używać starszego formatu z `ToolsVersion="15.0"`, przestrzenią nazw
MSBuild, pełną listą elementów `<Reference>`/`<Compile>` oraz plikiem
`packages.config`. Oba formaty są prawidłowe, ale nie wolno kopiować pojedynczych
fragmentów jednego formatu do drugiego.

W klasycznym projekcie plik `.vbproj` można otworzyć następująco:

1. kliknij projekt prawym przyciskiem w **Solution Explorer**;
2. wybierz **Unload Project**;
3. kliknij wyszarzony projekt prawym przyciskiem i wybierz **Edit SM_v3.vbproj**;
4. po zapisaniu wybierz **Reload Project**.

Referencjami NuGet w takim projekcie należy zarządzać przez **Manage NuGet Packages**
albo **Package Manager Console**, ponieważ NuGet aktualizuje równocześnie
`packages.config`, elementy `<Reference>` i ich `<HintPath>`. Ręczna zmiana samego
`HintPath` może pozostawić niespójne zależności. Pliki `.vb` otwiera się normalnie po
rozwinięciu projektu w Solution Explorer i dwukrotnym kliknięciu nazwy pliku; nie
trzeba do tego otwierać `.vbproj`.

Jeżeli rzeczywisty `.vbproj` zawiera drugi dokument XML po pierwszym `</Project>`,
należy usunąć całą powtórzoną część. Poprawny plik projektu ma dokładnie jeden element
główny `<Project>`. Jeśli powtórzenie pojawiło się tylko podczas kopiowania treści do
wiadomości, nie wymaga to żadnej zmiany.

### Visual Studio 2019 — metoda zalecana

1. W instalatorze Visual Studio wybierz obciążenie **Programowanie aplikacji klasycznych
   dla platformy .NET** oraz składnik **.NET Framework 4.8 targeting pack**.
2. Otwórz `SignatureManager.sln`.
3. Wybierz **Tools → NuGet Package Manager → Package Manager Settings** i upewnij
   się, że dostępne jest zatwierdzone źródło pakietów. W sieci firmowej może to być
   wewnętrzny mirror zamiast publicznego `nuget.org`.
4. Kliknij rozwiązanie prawym przyciskiem i wybierz **Restore NuGet Packages**.
5. Wybierz konfigurację `Release` i wykonaj **Build → Rebuild Solution**.

Visual Studio pobierze wersje zadeklarowane w projekcie wraz z ich zależnościami
przechodnimi. Pakiety trafią do globalnej pamięci podręcznej NuGet użytkownika, zwykle:

```text
%USERPROFILE%\.nuget\packages
```

### Developer Command Prompt

To samo można wykonać z **Developer Command Prompt for VS 2019**:

```bat
cd C:\sciezka\do\SF-REPO
msbuild SignatureManager.sln /t:Restore /p:RestoreIgnoreFailedSources=false
msbuild SignatureManager.sln /t:Rebuild /p:Configuration=Release
```

Plik wykonywalny zostanie utworzony w:

```text
SignatureManager\bin\Release\net48\SignatureManager.exe
```

Jeżeli firmowy serwer NuGet wymaga jawnego wskazania źródła, można przekazać je
podczas odtwarzania:

```bat
msbuild SignatureManager.sln /t:Restore /p:RestoreSources="https://adres-firmowego-nuget/v3/index.json"
```

Nie należy używać parametru `RestoreIgnoreFailedSources=true`, ponieważ mógłby ukryć
brak wymaganej biblioteki. W środowisku odizolowanym administrator powinien najpierw
zatwierdzić i skopiować do firmowego repozytorium wszystkie pakiety wskazane w projekcie
oraz ich zależności przechodnie.

### Ważne: licencja iText

Samo polecenie NuGet instaluje techniczne zależności, ale nie nadaje licencji na ich
użycie. Przed wdrożeniem zamkniętej aplikacji korporacyjnej należy uzyskać komercyjną
licencję iText albo uzyskać od działu prawnego potwierdzenie zgodności aplikacji z AGPL.
Pozostałe pakiety również powinny przejść standardową procedurę zatwierdzania
bezpieczeństwa i licencji obowiązującą w organizacji.

### Reset zależności po dużej liczbie błędów

Setki błędów kompilatora zwykle są błędami wtórnymi po jednym nieudanym
odtworzeniu NuGet. Należy zacząć od pierwszego błędu `NU...` lub pierwszego błędu
z pliku `restore.log`, zamiast analizować ostatni błąd na liście.

W repozytorium znajduje się skrypt, który usuwa wyłącznie lokalne wyniki kompilacji,
wymusza ponowne odtworzenie `PackageReference`, a dopiero po udanym odtworzeniu
wykonuje pełną przebudowę. Uruchom **Windows PowerShell** jako zwykły użytkownik:

```powershell
cd C:\sciezka\do\SF-REPO
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\Reset-Dependencies.ps1 -Configuration Release
```

Przed uruchomieniem należy zamknąć Visual Studio. Skrypt usuwa katalogi `.vs`,
`SignatureManager\bin` i `SignatureManager\obj`, ale domyślnie nie narusza globalnej
pamięci NuGet. Logi dwóch etapów zapisuje w:

```text
artifacts\dependency-reset\restore.log
artifacts\dependency-reset\rebuild.log
```

Jeżeli `restore.log` informuje o uszkodzonym pakiecie i zwykły reset nie pomaga,
można jednorazowo wyczyścić również globalną pamięć pakietów:

```powershell
.\scripts\Reset-Dependencies.ps1 -Configuration Release -ClearGlobalCache
```

Ta opcja usuwa `%USERPROFILE%\.nuget\packages`, czyli pamięć współdzieloną przez
inne projekty użytkownika, i dlatego nie powinna być pierwszym krokiem. Po jej użyciu
NuGet musi ponownie pobrać wszystkie pakiety. Jeżeli odtwarzanie nadal kończy się
błędem, należy sprawdzić w Visual Studio:

1. czy źródło NuGet jest włączone i osiągalne;
2. czy firmowy mirror zawiera dokładnie wersje wymienione w `.vbproj`;
3. czy zainstalowano .NET Framework 4.8 Targeting Pack;
4. jaki jest **pierwszy** błąd w `restore.log`.

Bez pierwszego błędu z `restore.log` nie da się wiarygodnie ustalić, czy przyczyną
jest brak dostępu do źródła, brak konkretnej wersji pakietu, certyfikat TLS/proxy czy
brak zestawu referencyjnego .NET Framework 4.8.

### Adapter kryptograficzny iText

Pakiet `Portable.BouncyCastle` nie zastępuje adaptera wymaganego przez iText.
Pakiety `itext7` oraz `itext.bouncy-castle-adapter` muszą mieć tę samą wersję.
Projekt używa wersji `9.7.0` obu pakietów. W **Package Manager Console** można
naprawić istniejący projekt poleceniami:

```powershell
Uninstall-Package Portable.BouncyCastle -Force
Uninstall-Package itext.bouncy-castle-adapter -Force
Uninstall-Package itext7 -Force
Install-Package itext7 -Version 9.7.0
Install-Package itext.bouncy-castle-adapter -Version 9.7.0
```

Po odinstalowaniu pakietów należy zamknąć Visual Studio, usunąć `.vs`, `bin` i `obj`,
a dopiero potem ponownie otworzyć rozwiązanie, odtworzyć pakiety i przebudować je.
Brak adaptera powoduje wyjątek `Either itext.bouncy-castle-adapter or
itext.bouncy-castle-fips-adapter dependency must be added` podczas wywołania
`SignatureUtil.ReadSignatureData`.

Błąd `manifestu zestawu nie odpowiada odwołaniu do zestawu (0x80131040)` oznacza,
że w katalogu wynikowym znalazły się biblioteki iText z różnych wersji. Nie należy
naprawiać go przez binding redirect. Trzeba usunąć stare DLL-e z `bin`/`obj` i mieć
jedną, identyczną wersję wszystkich pakietów iText. Program sprawdza to teraz przy
starcie i kończy się kodem `6`, zanim rozpocznie analizę setek dokumentów.

## Uruchomienie

```bat
SignatureManager.exe "C:\ROZSYLACZ\IN" "C:\ROZSYLACZ\OUT" "C:\ROZSYLACZ\SAP_AK_pesele.xlsx" "C:\ROZSYLACZ\wynik.xlsx"
```

## Dystrybucja jako pojedynczy plik EXE

Projekt używa `Fody` i `Costura.Fody`. Podczas kompilacji Release zależności zarządzane
(.NET DLL), między innymi iText, adapter Bouncy Castle i ClosedXML, są osadzane w
`SignatureManager.exe`. Plik `FodyWeavers.xml` musi znajdować się w tym samym katalogu
co `.vbproj` i zawierać:

```xml
<Weavers>
  <Costura />
</Weavers>
```

W klasycznym projekcie `SM_v3` z `packages.config` zainstaluj pakiety przez **Package
Manager Console** (z `SM_v3` wybranym jako Default project):

```powershell
Install-Package Fody -Version 6.9.3
Install-Package Costura.Fody -Version 6.2.0
```

Następnie dodaj `FodyWeavers.xml` do głównego katalogu projektu, wykonaj **Rebuild** w
konfiguracji `Release` i przetestuj kopię samego `SM_v3.exe` w pustym folderze na
komputerze testowym. Nie kopiuj EXE z `bin\Debug`, ponieważ pliki PDB i zachowanie
debuggera mogą zaciemniać test samodzielności.

Costura osadza biblioteki aplikacji, ale nie osadza .NET Framework ani nie zmienia
wymaganego środowiska uruchomieniowego. Komputer docelowy nadal musi mieć właściwą
wersję .NET Framework. Osadzenie bibliotek nie usuwa też obowiązków licencyjnych —
w szczególności wdrożenie iText nadal wymaga zgodności z AGPL albo licencji
komercyjnej. Pliki PDB i XML są opcjonalnymi artefaktami diagnostycznymi; do działania
aplikacji po poprawnym teście powinien wystarczyć sam EXE.

### Fody: `Could not find 'System.Object'`

W klasycznym projekcie .NET Framework 4.7.2 nie należy używać wcześniejszej pary
Fody `6.8.2` / Costura `6.0.0`, jeżeli podczas weavingu pojawia się błąd
`Could not find 'System.Object'`. Usuń obie wersje i zainstaluj zgodną parę używaną
przez projekt:

```powershell
Uninstall-Package Costura.Fody -Force
Uninstall-Package Fody -Force
Install-Package Fody -Version 6.9.3
Install-Package Costura.Fody -Version 6.2.0
```

Potem zamknij Visual Studio, usuń `.vs`, `bin` i `obj`, ponownie otwórz rozwiązanie i
wykonaj Rebuild w konfiguracji Release. Sprawdź też, czy `packages.config` nie zawiera
jednocześnie starych i nowych wersji oraz czy w `.vbproj` nie pozostały stare importy
Fody z katalogu `packages\Fody.6.8.2`.

Komunikat analizatora `IDE0060 Remove unused parameter 'args'` jest sugestią, a nie
błędem kompilacji. Parametr `args` jest potrzebny w wersji programu uruchamianej z
czterema argumentami i nie należy go usuwać.

W klasycznym `.vbproj` pakiety Fody nie muszą występować jako zwykłe referencje DLL.
Fody jest narzędziem etapu kompilacji, dlatego poprawne wpisy mają postać importów
`Fody.targets`, `Costura.Fody.props` i `Costura.Fody.targets` oraz kontroli
`EnsureNuGetPackageBuildImports`. Costura może dodatkowo występować jako `<Reference
Include="Costura">` z `HintPath`. Nie należy dopisywać drugiego kompletu tych wpisów.

Obecność pakietów na dysku można sprawdzić z katalogu rozwiązania:

```powershell
Test-Path ".\packages\Fody.6.9.3\build\Fody.targets"
Test-Path ".\packages\Costura.Fody.6.2.0\build\Costura.Fody.props"
Test-Path ".\packages\Costura.Fody.6.2.0\build\Costura.Fody.targets"
```

Wszystkie trzy polecenia powinny zwrócić `True`. Jeśli wpisy są w `.vbproj`, ale pliki
nie istnieją, problemem jest niepełne odtworzenie NuGet, a nie brak wpisów w projekcie.

Jeżeli wszystkie zwracają `False`, najpierw sprawdź, czy PowerShell pracuje w katalogu
projektu. W **Package Manager Console** można wyznaczyć go bez zgadywania:

```powershell
$projectDir = Split-Path (Get-Project SM_v3).FullName
$projectDir
Test-Path (Join-Path $projectDir "packages\Fody.6.9.3\build\Fody.targets")
Test-Path (Join-Path $projectDir "packages\Costura.Fody.6.2.0\build\Costura.Fody.props")
Test-Path (Join-Path $projectDir "packages\Costura.Fody.6.2.0\build\Costura.Fody.targets")
```

Jeśli również te testy zwracają `False`, pliki pakietów rzeczywiście nie zostały
odtworzone w lokalizacji zapisanej w `.vbproj`. Wtedy, z `SM_v3` ustawionym jako
**Default project**, wykonaj:

```powershell
Update-Package Fody -Reinstall -ProjectName SM_v3
Update-Package Costura.Fody -Reinstall -ProjectName SM_v3
```

Po instalacji powtórz testy oparte na `$projectDir`. Nie uruchamiaj Rebuild, dopóki
wszystkie trzy nie zwrócą `True`. Jeśli pliki zostaną znalezione gdzie indziej, nie
kopiuj ich ręcznie — należy poprawić/ponowić NuGet restore, aby `packages.config`,
`HintPath` i importy MSBuild pozostały spójne.

Jeżeli wszystkie trzy testy zwracają `True`, ale Fody nadal zgłasza `Could not find
'System.Object'`, pakiety są już poprawnie odnalezione, a problem dotyczy rozwiązywania
zestawów referencyjnych .NET Framework. Sprawdź obecność zestawu docelowego:

```powershell
$referenceRoot = "${env:ProgramFiles(x86)}\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2"
Test-Path (Join-Path $referenceRoot "mscorlib.dll")
Test-Path (Join-Path $referenceRoot "Facades\netstandard.dll")
```

Brak `mscorlib.dll` oznacza konieczność doinstalowania **.NET Framework 4.7.2
Developer Pack/Targeting Pack** albo ponownego ustawienia projektu na .NET Framework
4.8 wraz z zainstalowanym 4.8 Targeting Pack. Nie należy mieszać targetu 4.7.2 z
losowo skopiowanymi zestawami referencyjnymi 4.8.

Jeżeli oba pliki istnieją, w klasycznym projekcie VB można jawnie dodać do głównej
grupy `<ItemGroup>` z referencjami wpis `<Reference Include="mscorlib" />`, następnie
usunąć `bin`/`obj` i przebudować projekt. Nie dodawaj `HintPath` do `mscorlib` — MSBuild
powinien wybrać go z Targeting Pack zgodnego z `TargetFrameworkVersion`.

Jeżeli błąd pozostaje, uruchom z **Developer Command Prompt for VS 2019** kompilację
diagnostyczną:

```bat
msbuild SM_v3.vbproj /t:Rebuild /p:Configuration=Release /v:diag > fody-build.log
```

Do dalszej diagnozy potrzebny jest fragment `fody-build.log` od `FodyTarget` do
komunikatu `Could not find 'System.Object'`; sama lista błędów Visual Studio nie
zawiera ścieżek referencyjnych przekazanych do Fody.

Program rekursywnie przetwarza pliki z rozszerzeniem `.pdf` (bez względu na wielkość liter), zapisuje jeden wiersz raportu na każdy znaleziony PDF, a po poprawnym zapisaniu raportu przenosi wszystkie PDF-y do katalogu wyjściowego. Pliki inne niż PDF pozostają bez zmian.

Raport zawiera kolumny `Numer pracownika`, `Data dokumentu`, `Tytuł / nazwa pliku`,
`Status podpisu` i `Data podpisu`. `Data podpisu` oznacza wyłącznie najnowszą datę
ważnego podpisu rozpoznanego jako podpis pracownika; podpisy kierownika nie wpływają
na tę kolumnę. Data jest prezentowana w formacie `yyyy-MM-dd`.

Jeżeli metadana PDF `/Title` jest pusta, status ma wartość `Pusty tytuł`, kolumny
`Numer pracownika` i `Data dokumentu` pozostają puste, a w kolumnie `Tytuł / nazwa
pliku` zapisywana jest fizyczna nazwa przetwarzanego PDF-a. Nazwa pliku jest zapisywana
w tej kolumnie również dla statusu `Brak odczytu`.

Podczas przenoszenia do `OUT` dla statusów `Błędny tytuł`, `Pusty tytuł` i `Brak
odczytu` podstawą nazwy pozostaje fizyczna nazwa pliku z `IN`. Dla wszystkich
pozostałych statusów podstawą nazwy jest metadana PDF `/Title`. W obu przypadkach
program usuwa końcowe rozszerzenie, dodaje stempel `__yyyyMMddHHmmss_######`, a
następnie rozszerzenie `.pdf`; kolizje otrzymują dodatkowo `_v2`, `_v3` itd.

## Kody zakończenia

| Kod | Znaczenie |
|---:|---|
| 0 | Sukces |
| 1 | Nieprawidłowa liczba argumentów |
| 2 | Brak folderu wejściowego |
| 3 | Brak pliku SAP_AK |
| 4 | Nie udało się zapisać raportu |
| 5 | Raport zapisano, ale co najmniej jednego PDF-a nie udało się przenieść |
| 6 | Brak adaptera kryptograficznego iText albo niezgodne wersje bibliotek iText |
| 10 | Krytyczny błąd programu |

Log jest zapisywany obok raportu jako `SignatureManager_yyyyMMdd_HHmmss.log`. Dane PESEL i adresy e-mail nie są umieszczane w logu.

### Błąd licencji EPPlus

Generator raportu używa teraz wyłącznie ClosedXML. EPPlus został usunięty, ponieważ
EPPlus 8 wymaga jawnego skonfigurowania licencji i w przeciwnym razie zgłasza
`OfficeOpenXml.LicenseNotSetException` jeszcze przed utworzeniem arkusza. W klasycznym
projekcie korzystającym z `packages.config` należy wykonać:

```powershell
Uninstall-Package EPPlus -Force
```

Nie należy usuwać `ClosedXML`, ponieważ służy teraz zarówno do odczytu `SAP_AK`, jak
i do zapisu raportu. Po usunięciu EPPlus trzeba zamknąć Visual Studio, usunąć `bin`
i `obj`, ponownie otworzyć projekt i wykonać Rebuild. Nie ma potrzeby ustawiania
`ExcelPackage.License`, ponieważ kod nie tworzy już obiektów EPPlus.

## Diagnostyka uruchamiania z Visual Studio

Komunikat Visual Studio `New debugging session can't be started until the existing
one has ended` pochodzi z debuggera Roslyn/Edit and Continue, a nie z kodu
Signature Manager. Oznacza on, że podjęto próbę uruchomienia kolejnej sesji, gdy
poprzednia aplikacja nadal działa albo Visual Studio nie zakończyło jej poprawnie.

1. Nie naciskaj ponownie `F5`, jeśli pierwsze uruchomienie nadal analizuje pliki.
2. Wybierz **Debug → Stop Debugging** (`Shift+F5`).
3. Sprawdź w Menedżerze zadań, czy nie działa nadal `SignatureManager.exe` albo
   `SM_v3.exe`; zakończ pozostawiony proces tylko wtedy, gdy rzeczywiście się zawiesił.
4. Zamknij i ponownie uruchom Visual Studio. Jeżeli problem debuggera wraca, zamknij
   Visual Studio, usuń katalog `.vs` rozwiązania i ponownie otwórz rozwiązanie.
5. Do testu produkcyjnego uruchom EXE jeden raz z `cmd.exe`, zamiast wielokrotnie
   naciskać `F5`.

Raport powstaje dopiero po zakończeniu analizy wszystkich znalezionych PDF-ów.
Przenoszenie plików zaczyna się dopiero po poprawnym zapisaniu raportu. Jeżeli zapis
raportu się nie powiedzie, program zwraca kod `4` i zgodnie z przyjętą kolejnością nie
przenosi żadnego PDF-a. Dlatego brak raportu i brak plików w `OUT` należy diagnozować
na podstawie końca najnowszego logu:

```powershell
Get-Content "C:\sciezka\do\SignatureManager_yyyyMMdd_HHmmss.log" -Tail 50
```

Znaczenie ostatniego wpisu:

- `Analiza: ...; status: ...` — program nadal analizował pliki albo został zatrzymany
  przed utworzeniem raportu;
- `Nie udało się zapisać raportu` — szczegóły wyjątku bezpośrednio pod tym wpisem
  wskazują właściwą przyczynę, np. otwarty `wynik.xlsx`, brak uprawnień albo brakującą
  bibliotekę;
- `Zapisano raport: ...` — raport powstał i program powinien przejść do przenoszenia;
- `Nie udało się przenieść pliku` — raport powstał, ale wskazanego PDF-a nie udało się
  przenieść;
- `Zakończono przetwarzanie` — całe wykonanie doszło do końca.

Przed uruchomieniem trzeba zamknąć istniejący `wynik.xlsx` w Excelu. Cztery argumenty
wiersza poleceń należy ustawić w **Project → Properties → Debug → Command line
arguments** albo przekazać bezpośrednio w `cmd.exe`.
