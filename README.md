# PdfSignatureReport

Konsolowa aplikacja VB.NET dla Visual Studio 2019, która skanuje pliki PDF z katalogu przekazanego jako pierwszy parametr, odczytuje informacje o podpisach cyfrowych i zapisuje raport XLSX do pliku przekazanego jako drugi parametr.

## Zakres raportu

Każdy plik PDF jest zapisywany w jednym wierszu arkusza `Podpisy PDF`. W kolumnach znajdują się m.in.:

- nazwa pliku PDF,
- liczba podpisów,
- wykryte zdublowane podpisy w tym samym pliku,
- PESEL,
- VAT/NIP,
- nazwisko i imię osoby podpisanej,
- data podpisu,
- typ podpisu,
- nazwa pola podpisu,
- informacja, czy podpis obejmuje cały dokument,
- status odczytu, status certyfikatu oraz ewentualne błędy.

Jeśli plik zawiera kilka podpisów, wartości dla podpisów są wpisywane w tym samym wierszu i rozdzielane separatorem ` | `.

## Uruchomienie

1. Otwórz `PdfSignatureReport.sln` w Visual Studio 2019.
2. Przywróć pakiety NuGet.
3. Zbuduj projekt.
4. Uruchom program z dwoma parametrami:

   ```cmd
   PdfSignatureReport.exe "C:\plikiPDF" "C:\raporty\RaportPodpisowPDF.xlsx"
   ```

   - pierwszy parametr: ścieżka do katalogu z plikami PDF,
   - drugi parametr: ścieżka do wynikowego pliku `.xlsx`.

5. Jeśli katalog dla pliku wynikowego nie istnieje, program utworzy go automatycznie.
6. W tym samym katalogu co plik wynikowy `.xlsx` program zapisze plik `log.txt`. Log zawiera błędy odczytu PDF, błędne podpisy, zdublowane podpisy, błędne certyfikaty, nieważne certyfikaty oraz inne błędy programu.

## Biblioteki NuGet

- `itextsharp` — odczyt podpisów cyfrowych z PDF.
- `ClosedXML` — zapis raportu XLSX.
